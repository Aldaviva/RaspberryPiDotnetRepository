using System.Security.Cryptography;

namespace RaspberryPiDotnetRepository.Unfucked.System.Security.Cryptography;

public static class RandomStringGenerator {

    // https://stackoverflow.com/a/73101585/979493
    public static string getString(uint length, string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789") {
        char[] distinctAlphabet       = alphabet.Distinct().ToArray();
        int    distinctAlphabetLength = distinctAlphabet.Length;
        char[] result                 = new char[length];
        for (int i = 0; i < length; i++) {
            result[i] = distinctAlphabet[RandomNumberGenerator.GetInt32(distinctAlphabetLength)];
        }

        return new string(result);
    }

}