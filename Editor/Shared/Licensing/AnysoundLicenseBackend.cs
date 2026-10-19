using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Anysound.Shared.Licensing
{
    public enum AnysoundLicenseRequestStatus
    {
        Ok,
        UnknownKey,
        Failed,
    }

    public struct AnysoundLicenseResponse
    {
        public AnysoundLicenseRequestStatus status;
        // On Ok: {"payload": base64 of the entitlements JSON, "signature": base64 RSA-SHA256 PKCS#1 v1.5 signature of those bytes}
        public string body;
        public string error;
    }

    // Asks for the signed list of products a key holds. Calls back on the main thread
    public interface IAnysoundLicenseBackend
    {
        void RequestEntitlements(string key, Action<AnysoundLicenseResponse> done);
    }

    // POST {"key": "..."} to /api/entitlements. 200 is a signed list, 404 an unknown key, 429 rate limited
    class AnysoundHttpLicenseBackend : IAnysoundLicenseBackend
    {
        [Serializable]
        struct KeyRequest
        {
            public string key;
        }

        readonly string _url;

        public AnysoundHttpLicenseBackend(string url) => _url = url;

        public void RequestEntitlements(string key, Action<AnysoundLicenseResponse> done)
        {
            var request = new UnityWebRequest(_url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new KeyRequest { key = key }))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 15,
            };
            request.SetRequestHeader("Content-Type", "application/json");

            request.SendWebRequest().completed += _ =>
            {
                var response = new AnysoundLicenseResponse();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    response.status = AnysoundLicenseRequestStatus.Ok;
                    response.body = request.downloadHandler.text;
                }
                else if (request.responseCode == 404)
                {
                    response.status = AnysoundLicenseRequestStatus.UnknownKey;
                    response.error = "This key wasn't found.";
                }
                else
                {
                    response.status = AnysoundLicenseRequestStatus.Failed;
                    response.error = request.responseCode == 429
                        ? "Too many attempts. Try again in a few minutes."
                        : $"Couldn't reach the license server ({request.error}).";
                }

                request.Dispose();
                done(response);
            };
        }
    }
}
