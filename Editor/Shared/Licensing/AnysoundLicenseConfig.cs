using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Anysound.Shared.Licensing
{
    // The products a key can hold. The same ids are used in the entitlements table and on the purchase page
    public static class AnysoundProducts
    {
        public const string Footsteps = "footsteps";
        public const string Whoosh = "whoosh";
        public const string UI = "ui";
        public const string Retro = "retro";

        static readonly Dictionary<string, string> DisplayNames = new()
        {
            { Footsteps, "Footsteps" },
            { Whoosh, "Whoosh" },
            { UI, "UI" },
            { Retro, "8-Bit" },
        };

        public static string DisplayName(string productId) =>
            productId != null && DisplayNames.TryGetValue(productId, out var displayName) ? displayName : productId;
    }

    static class AnysoundLicenseConfig
    {
        // Answers in the editor with test keys instead of calling the server.
        // Set to false once /api/entitlements is deployed and the production public key is filled in below
        public static readonly bool UsePlaceholderBackend = false;

        public const string EntitlementsUrl = "https://anysound.vercel.app/api/entitlements";
        // The page where a user gets a free key with their email
        public const string GetKeyUrl = "https://anysound.vercel.app/";

        // How often the browser fetches a new signed list. The list itself stays valid until it expires
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);

        // The public half of the key Vercel signs with (JWK "n" and "e", as standard base64)
        const string ProductionModulus = "q6xj8/oPsoZF4aG9cCvAWeizszebjzDYnsVNhGSbcJIv2mIJXxywG1LJBmDOl7n4e0ujeije/ZRYtkaDQXsGQ37+MG91JtRFsIqtMuZ911m4TyKO6I5qQVOED+Sa7Cr04nLAv7rnDAzW5TNtY0pzgJuQFG22+DfF0KYXYOfzpc8v0xWmsdJeuLjVT3tTAhOQw+Hv0F0XGAlPuNTizVsl0HIVEdulNMdloa0J8FmKB+OFfnjrcohluQdAqXsn9+ABjO46hixrULxernATIgX2GZPHyFT7PLL+nkv/3suFec4b4bzz93c8sQ933/ZqQEsNvyZIgp4prGPl1Ri6+CRvOw==";
        const string ProductionExponent = "AQAB";

        public static IAnysoundLicenseBackend Backend =>
            UsePlaceholderBackend ? new AnysoundPlaceholderLicenseBackend() : new AnysoundHttpLicenseBackend(EntitlementsUrl);

        public static RSAParameters PublicKey => UsePlaceholderBackend
            ? AnysoundPlaceholderLicenseBackend.PublicKey
            : new RSAParameters
            {
                Modulus = Convert.FromBase64String(ProductionModulus),
                Exponent = Convert.FromBase64String(ProductionExponent),
            };
    }
}
