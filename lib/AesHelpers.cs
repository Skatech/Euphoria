using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System.Reflection.Metadata;
using System.Windows.Input;

namespace Skatech.IO;

static class AesHelpers {
    const int SALT_SIZE = 8, IVEC_SIZE = 16, KVEC_SIZE = 32;
    static readonly byte[] SALT_ZERO = Enumerable.Repeat<byte>(0x00, SALT_SIZE).ToArray(),
        IVEC_ZERO = Enumerable.Repeat<byte>(0x00, IVEC_SIZE).ToArray();
    public const string DefaultFileExtension = "aes";
    
    public static byte[] GenerateSalt() {
        return RandomNumberGenerator.GetBytes(SALT_SIZE);
    }

    public static byte[] GenerateIV() {
        return RandomNumberGenerator.GetBytes(IVEC_SIZE);
    }

    public static byte[] GenerateKey(string password, int iterations, byte[]? salt) {
        using var deriver = new Rfc2898DeriveBytes(password, salt ?? SALT_ZERO, iterations, HashAlgorithmName.SHA1);
        return deriver.GetBytes(KVEC_SIZE);
    }

    public static Stream CreateDecryptStream(string filePath, bool isDeflated, byte[] key, byte[]? iv = null) {
        using var alg = Aes.Create();
        alg.Key = key;
        alg.IV = iv ?? IVEC_ZERO;
        var stream = new CryptoStream(
            File.OpenRead(filePath), alg.CreateDecryptor(), CryptoStreamMode.Read, false);
        return isDeflated
            ? new DeflateStream(stream, CompressionMode.Decompress, false)
            : stream;
    }

    public static Stream CreateEncryptStream(string filePath, bool isDeflated, byte[] key, byte[]? iv = null) {
        using var alg = Aes.Create();
        alg.Key = key;
        alg.IV = iv ?? IVEC_ZERO;
        var stream = new CryptoStream(
            File.Create(filePath), alg.CreateEncryptor(), CryptoStreamMode.Write, false);
        return isDeflated
            ? new DeflateStream(stream, CompressionLevel.Optimal, false)
            : stream;
    }
    
    public static void Decrypt(string archiveFile, string outputFile, bool isDeflated, byte[] key, byte[]? iv = null) {
        using var dstream = CreateDecryptStream(archiveFile, isDeflated, key, iv);
        using var fstream = File.Create(outputFile);
        dstream.CopyTo(fstream);
    }

    public static void Encrypt(string inputFile, string archiveFile, bool isDeflated, byte[] key, byte[]? iv = null) {
        using var estream = CreateEncryptStream(archiveFile, isDeflated, key, iv);
        using var fstream = File.OpenRead(inputFile);
        fstream.CopyTo(estream);
    }

    public static IEnumerable<string> DecryptLines(
            string archiveFile, Encoding encoding, bool isDeflated, byte[] key, byte[]? iv = null) {
        using var stream = CreateDecryptStream(archiveFile, isDeflated, key, iv);
        using var reader = new StreamReader(stream, encoding);
        while(reader.ReadLine() is string line)
            yield return line;
    }

    public static void EncryptLines(
            string archiveFile, IEnumerable<string> lines, Encoding encoding, bool isDeflated, byte[] key, byte[]? iv) {
        using var stream = CreateEncryptStream(archiveFile, isDeflated, key, iv);
        using var writer = new StreamWriter(stream, encoding);
        foreach (var line in lines)
            writer.WriteLine(line);
    }
}
