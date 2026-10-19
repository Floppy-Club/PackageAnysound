using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Anysound.Shared.Licensing
{
    // The signed contents of a list: which products a key held when the server issued it, and until when the list is valid
    [Serializable]
    public class AnysoundEntitlements
    {
        public string key;
        public string[] products;
        public long issued;
        public long expires;
    }

    [Serializable]
    class AnysoundSignedEntitlements
    {
        public string payload;
        public string signature;
    }

    public enum AnysoundLicenseState
    {
        NoKey,
        // A key is entered but there is no list for it yet, e.g. it was never checked online
        NotChecked,
        Valid,
        Expired,
    }

    // The user's key and the products it unlocks. The key and the last signed list are kept in EditorPrefs,
    // so they apply to every project on this machine and never end up in version control.
    // The list is verified against the public key on every load, so it works offline until it expires
    public static class AnysoundLicense
    {
        const string KeyPref = "Anysound.License.Key";
        const string ListPref = "Anysound.License.SignedList";
        const string LastRefreshPref = "Anysound.License.LastRefresh";

        static bool _loaded;
        static string _key;
        static AnysoundEntitlements _entitlements;

        // Raised on the main thread whenever the key or the unlocked products may have changed
        public static event Action Changed;

        public static bool IsRefreshing { get; private set; }
        public static string LastError { get; private set; }

        public static string Key
        {
            get
            {
                EnsureLoaded();
                return _key;
            }
        }

        public static AnysoundLicenseState State
        {
            get
            {
                EnsureLoaded();
                if (string.IsNullOrEmpty(_key)) return AnysoundLicenseState.NoKey;
                if (_entitlements == null) return AnysoundLicenseState.NotChecked;
                return IsExpired(_entitlements) ? AnysoundLicenseState.Expired : AnysoundLicenseState.Valid;
            }
        }

        // The products the current list unlocks, empty if there is no valid list
        public static string[] Products =>
            State == AnysoundLicenseState.Valid ? _entitlements.products ?? new string[0] : new string[0];

        public static DateTimeOffset? LastRefresh
        {
            get
            {
                string value = EditorPrefs.GetString(LastRefreshPref, "");
                return long.TryParse(value, out long seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
            }
        }

        public static DateTimeOffset? Expires =>
            _entitlements != null ? DateTimeOffset.FromUnixTimeSeconds(_entitlements.expires) : null;

        // Free generators have no product and are always unlocked
        public static bool IsUnlocked(string productId) =>
            string.IsNullOrEmpty(productId) || Products.Contains(productId);

        // Saves a new key and checks it online. Returns false if the text isn't a key
        public static bool SetKey(string input, Action<bool> done = null)
        {
            string key = AnysoundLicenseKey.Normalize(input);
            if (key == null) return false;

            EnsureLoaded();
            if (key != _key)
            {
                _key = key;
                EditorPrefs.SetString(KeyPref, key);
                // The old list belongs to another key
                ClearList();
                LastError = null;
                Changed?.Invoke();
            }

            Refresh(done);
            return true;
        }

        public static void RemoveKey()
        {
            _key = null;
            EditorPrefs.DeleteKey(KeyPref);
            ClearList();
            LastError = null;
            Changed?.Invoke();
        }

        // Checks online now when it's been a while, or when the list is missing or expired
        public static void RefreshIfStale()
        {
            var state = State;
            if (state == AnysoundLicenseState.NoKey || IsRefreshing) return;
            var lastRefresh = LastRefresh;
            bool stale = lastRefresh == null || DateTimeOffset.UtcNow - lastRefresh.Value > AnysoundLicenseConfig.RefreshInterval;
            if (stale || state != AnysoundLicenseState.Valid)
                Refresh();
        }

        // Fetches a new signed list for the key. A failed request keeps the current list, so a key keeps working offline
        public static void Refresh(Action<bool> done = null)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(_key))
            {
                done?.Invoke(false);
                return;
            }

            if (IsRefreshing)
            {
                done?.Invoke(false);
                return;
            }

            IsRefreshing = true;
            string key = _key;
            Changed?.Invoke();

            AnysoundLicenseConfig.Backend.RequestEntitlements(key, response =>
            {
                IsRefreshing = false;
                // The key was changed or removed while waiting
                if (key != _key)
                {
                    done?.Invoke(false);
                    return;
                }

                bool success = HandleResponse(key, response);
                Changed?.Invoke();
                done?.Invoke(success);
            });
        }

        static bool HandleResponse(string key, AnysoundLicenseResponse response)
        {
            switch (response.status)
            {
                case AnysoundLicenseRequestStatus.Ok:
                    var entitlements = Verify(response.body, key);
                    if (entitlements == null)
                    {
                        LastError = "The license server's answer couldn't be verified.";
                        return false;
                    }

                    _entitlements = entitlements;
                    EditorPrefs.SetString(ListPref, response.body);
                    EditorPrefs.SetString(LastRefreshPref, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                    LastError = null;
                    return true;

                case AnysoundLicenseRequestStatus.UnknownKey:
                    ClearList();
                    LastError = response.error;
                    return false;

                default:
                    LastError = response.error;
                    return false;
            }
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _key = AnysoundLicenseKey.Normalize(EditorPrefs.GetString(KeyPref, ""));
            if (_key == null) return;
            string signedList = EditorPrefs.GetString(ListPref, "");
            if (signedList.Length > 0)
                _entitlements = Verify(signedList, _key);
        }

        // The entitlements in a signed list, or null if the signature doesn't hold or the list is for another key
        static AnysoundEntitlements Verify(string signedListJson, string key)
        {
            try
            {
                var signedList = JsonUtility.FromJson<AnysoundSignedEntitlements>(signedListJson);
                if (signedList?.payload == null || signedList.signature == null) return null;

                byte[] payload = Convert.FromBase64String(signedList.payload);
                byte[] signature = Convert.FromBase64String(signedList.signature);

                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(AnysoundLicenseConfig.PublicKey);
                    if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        return null;
                }

                var entitlements = JsonUtility.FromJson<AnysoundEntitlements>(Encoding.UTF8.GetString(payload));
                return AnysoundLicenseKey.Normalize(entitlements?.key) == key ? entitlements : null;
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is CryptographicException)
            {
                return null;
            }
        }

        static bool IsExpired(AnysoundEntitlements entitlements) =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= entitlements.expires;

        static void ClearList()
        {
            _entitlements = null;
            EditorPrefs.DeleteKey(ListPref);
            EditorPrefs.DeleteKey(LastRefreshPref);
        }
    }
}
