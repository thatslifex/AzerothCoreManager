using System.Security.Cryptography;
using System.Numerics;

namespace AzerothCoreManager.Services;

public static class Srp6Helper
{
    // AzerothCore SRP6 constants
    private static readonly BigInteger G = 7;
    private static readonly BigInteger N = BigInteger.Parse("0894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7",
        System.Globalization.NumberStyles.HexNumber);

    public static byte[] GenerateSalt()
    {
        var salt = new byte[32];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    public static byte[] ComputeVerifier(string username, string password, byte[] salt)
    {
        // h1 = SHA1("USERNAME:PASSWORD") — both UPPERCASE
        var h1 = SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{username.ToUpper()}:{password.ToUpper()}"));

        // h2 = SHA1(salt || h1) — binary concatenation
        var h2 = SHA1.HashData(salt.Concat(h1).ToArray());

        // Interpret h2 as little-endian BigInteger
        var h2Int = new BigInteger(h2, isUnsigned: true, isBigEndian: false);

        // verifier = (g ^ h2) % N
        var verifier = BigInteger.ModPow(G, h2Int, N);

        // Output as 32-byte little-endian
        var verifierBytes = verifier.ToByteArray(isUnsigned: true, isBigEndian: false);
        if (verifierBytes.Length > 32)
            verifierBytes = verifierBytes[..32];
        else if (verifierBytes.Length < 32)
            verifierBytes = verifierBytes.Concat(new byte[32 - verifierBytes.Length]).ToArray();

        return verifierBytes;
    }
}
