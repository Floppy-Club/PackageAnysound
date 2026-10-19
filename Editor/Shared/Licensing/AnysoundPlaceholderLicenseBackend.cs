using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Anysound.Shared.Licensing
{
    // Stands in for /api/entitlements until it is deployed: it knows a few test keys and signs their lists
    // in the editor with a test key pair, so the rest of the system runs exactly as it will against the server.
    // The test private key is here on purpose. It is not the production key and must never be used to sign real lists
    class AnysoundPlaceholderLicenseBackend : IAnysoundLicenseBackend
    {
        public static readonly Dictionary<string, string[]> TestKeys = new()
        {
            { "ANYS-TEST0-00000-00000-00000-00000A", new[] { AnysoundProducts.Footsteps, AnysoundProducts.Whoosh, AnysoundProducts.UI, AnysoundProducts.Retro } },
            { "ANYS-TEST0-00000-00000-00000-00000W", new[] { AnysoundProducts.Whoosh } },
            { "ANYS-TEST0-00000-00000-00000-00000F", new[] { AnysoundProducts.Footsteps } },
            { "ANYS-TEST0-00000-00000-00000-00000N", new string[0] },
        };

        static readonly TimeSpan ListLifetime = TimeSpan.FromDays(30);

        const string Modulus = "4+rQLjEJgtlQSp8oyxe38cnBXxhCRb0R32ghIHlyEoKnpU+QUXG/o8dvaelVdNBOdkcG6RKV8vLYnTVj52Q4kGHEjfrezAPv+vrQrwfD5RhYqIopcnCXc70jYwWX6ACco9k3D7o0xMuqmA4Dem9bnH7WZ5/VXuFIOgINE8eZtZyDeOGm0Y5hFpdubb9icHBs6OMTAUfuM8M/t09SIljEZrJhLB7TWUxI7kwKPJ8fo+xpOMCg92SnluwX+kh+cZ2TCJVkGexrBf/O4lBmFTwFjKm4zgNJxg9z9sMHRume8L+BWX1FN6qRmRQgDzn3S4qA+yCFz9Gyh0lLJyb1WidFTQ==";
        const string Exponent = "AQAB";
        const string D = "D2TcQ8pvHIXmDOw8Gp+S/oNEitI+I6y2CNNlLWbWhBaMETfTBRU8PukpBsvKSFYLfqSkfFiThTHO3KtvAIF96o6X8okHxFrduiN3teK94frnfha63u5o5FnZioYlsPddnRl+RRMsHkCtWJDVvk7ILsVIYP5kMIDh2n0p+fSI1sHHWGz/zduE0N9/ROuNJQdpbJJXvqKWrY/9DDI0Ljvxh+/r7mvU77PI58ueca7MajBu0qZGlnWY64Ih4Mb2mDip1Xit1HPg3f7km+biHDDcKOtHnR5pz8Bi+Qkup2E6kr7Iu3qYMtNaKpxU5aXTyIDAhP28OqIqiKMHvDCqMI238Q==";
        const string P = "/rcWP6jOVWS2rcn8BhoIN04AT7OBGcgWIcEhxruI94NgFmXySHFuEOpweCu/LjP91m9v/ZwIFuIxbFEXNWngTDYGf0kwTJXV4WgGS+NnqxusI4jFf4bGP0QLo+IISIe+1UzB40/dupn2+9OPEDIBQE4N6myhBL6vRZ7IeYiwf/k=";
        const string Q = "5REfRvRGyae9LToVMPyWn51i2Ietewe69jgmwZheK8y2F2XsgA9bBGxGUT77o8R+SfggIYk2fSbyg+888jYixXDvOhy3TNtX12WsGbpKFP8BFIKSfcC8G6kizZKiINQ0jM3vToHHZ2iwYpuSTG8R4gvgQAwKhymG4tlIr/iuLPU=";
        const string DP = "WazZv2XN5tCDZCIFgZxuohHrGCRJYmtIJ3AYVSA3M3TkQl538ztEFd6D5Dr6nuidCH7JPc9OJchSwCC4fAyfdBVdKjj5MS1bVHVMKjGQkPQ4IXaImcI4T5PESVlNs0NfUzcn18CJfqWwB67O4lPIjvwr+AlwkvwTNv6U8/zErvk=";
        const string DQ = "GdY0kOw8j8I7tD445a2Xz6kQ3Rtwlc/pZpwuwudMt+P/JeR5KHiS64zK4Z8ZAv/GvtEdzqqLWtJpyDfTx/4TLfHvCb7zbdfPsJvnLX+1m2in1ohjvYtfrD8HjehcIRfOVZNkypYYx7EnM/gYKEbe3YA5av/wsA4fCLc5W5/RBYE=";
        const string InverseQ = "zN74S/Yo4AB+hlh7NBnTrzINzGpXwzktIVPISQKHjG0PVD7phijsrkIubbH9VPLd7/2wGRuWZ6qyVbEbMtszNRAWELOqGnA9DDqI8PggLDIsPDIJEpRJMSLUtzhkH1TJLh/heD3zCDA46hbu5EaOgC8ixT2rZZ1pMu04ml9mUMk=";

        public static RSAParameters PublicKey => new()
        {
            Modulus = Convert.FromBase64String(Modulus),
            Exponent = Convert.FromBase64String(Exponent),
        };

        static RSAParameters PrivateKey => new()
        {
            Modulus = Convert.FromBase64String(Modulus),
            Exponent = Convert.FromBase64String(Exponent),
            D = Convert.FromBase64String(D),
            P = Convert.FromBase64String(P),
            Q = Convert.FromBase64String(Q),
            DP = Convert.FromBase64String(DP),
            DQ = Convert.FromBase64String(DQ),
            InverseQ = Convert.FromBase64String(InverseQ),
        };

        public void RequestEntitlements(string key, Action<AnysoundLicenseResponse> done)
        {
            var response = new AnysoundLicenseResponse();
            if (TestKeys.TryGetValue(key, out var products))
            {
                response.status = AnysoundLicenseRequestStatus.Ok;
                response.body = Sign(key, products);
            }
            else
            {
                response.status = AnysoundLicenseRequestStatus.UnknownKey;
                response.error = "This key wasn't found. The placeholder backend only knows the test keys.";
            }

            // Answer later, like a real request would
            EditorApplication.delayCall += () => done(response);
        }

        static string Sign(string key, string[] products)
        {
            var now = DateTimeOffset.UtcNow;
            var entitlements = new AnysoundEntitlements
            {
                key = key,
                products = products,
                issued = now.ToUnixTimeSeconds(),
                expires = now.Add(ListLifetime).ToUnixTimeSeconds(),
            };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(entitlements));

            using var rsa = RSA.Create();
            rsa.ImportParameters(PrivateKey);
            byte[] signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return JsonUtility.ToJson(new AnysoundSignedEntitlements
            {
                payload = Convert.ToBase64String(payload),
                signature = Convert.ToBase64String(signature),
            });
        }
    }
}
