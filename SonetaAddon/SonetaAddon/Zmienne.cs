using Soneta.Business.App;
using Soneta.Business.Licence;

namespace SonetaAddon
{
    /// <summary>
    /// Klasa <c>Zmienne</c> zawiera dane i metody pomocnicze do weryfikacji licencji dodatku w systemie Soneta.
    /// </summary>
    internal static class Zmienne
    {
        /// <summary>
        /// Nazwa firmy używana do identyfikacji licencji dodatku.
        /// </summary>
        static string _company = "HUBER Eryk Gałan";

        /// <summary>
        /// Nazwa modułu (rozwiązania). Wartość ta powinna zostać podmieniona na właściwą nazwę modułu.
        /// </summary>
        static string _module = "NAZWA ROZWIĄZANIA";  // <------tu do podmiany

        /// <summary>
        /// Klucz publiczny używany do weryfikacji licencji w systemie enova.
        /// </summary>
        internal static byte[] enovaPublicKey = new byte[] { 6, 2, 0, 0, 0, 164, 0, 0, 82, 83, 65, 49, 0, 4, 0, 0, 1, 0, 1, 0, 49, 119, 65, 150, 40, 157, 78, 141, 187, 215, 58, 101, 65, 107, 165, 32, 112, 241, 144, 189, 0, 18, 148, 243, 44, 31, 187, 202, 250, 2, 14, 66, 44, 181, 133, 244, 221, 158, 235, 239, 42, 135, 39, 130, 146, 38, 62, 190, 235, 117, 158, 210, 112, 236, 165, 231, 166, 206, 117, 116, 253, 19, 52, 136, 255, 252, 253, 83, 241, 117, 189, 65, 76, 92, 95, 212, 149, 168, 7, 209, 169, 242, 68, 25, 21, 229, 225, 154, 181, 205, 15, 17, 78, 116, 44, 65, 140, 123, 203, 250, 193, 187, 133, 195, 183, 71, 176, 157, 153, 62, 232, 101, 112, 247, 76, 255, 143, 65, 140, 38, 225, 164, 39, 40, 127, 245, 160, 160 };

        /// <summary>
        /// Rozszerzenie metody <c>CheckAddinLicence</c> dla klasy <see cref="Login"/>.
        /// Służy do weryfikacji licencji dodatku na podstawie zdefiniowanych danych.
        /// </summary>
        /// <param name="login">Obiekt <see cref="Login"/>, dla którego wykonywana jest weryfikacja licencji.</param>
        /// <exception cref="LicenceException">
        /// Rzucany, jeśli licencja jest nieprawidłowa, a parametr <c>throwErr</c> jest ustawiony na <c>true</c>.
        /// </exception>
        internal static void CheckAddinLicence(this Login login)
        {
            login.CheckAddinLicence(company: _company, module: _module, publicKey: enovaPublicKey, throwErr: true);
        }
    }
}
