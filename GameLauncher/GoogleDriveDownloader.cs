using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Web;

namespace GameLauncher
{
    static class GoogleDriveHelperHttpClient
        {
            private static readonly HttpClientHandler Handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            private static readonly HttpClient Client = new HttpClient(Handler);

            public static async Task DownloadFileAsync(string fileId, string destinationPath)
            {
                string baseUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

                // Étape 1 : Télécharger la page d'avertissement
                var htmlPage = await Client.GetStringAsync(baseUrl);

                // Étape 2 : Parser la page HTML
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlPage);

                var formNode = doc.DocumentNode.SelectSingleNode("//form[@id='download-form']");
                if (formNode == null)
                    throw new Exception("Formulaire de téléchargement introuvable sur la page Google Drive.");

                string actionUrl = formNode.GetAttributeValue("action", null!);
                if (string.IsNullOrEmpty(actionUrl))
                    throw new Exception("Attribut 'action' manquant dans le formulaire de téléchargement.");

                var inputs = formNode.SelectNodes(".//input[@type='hidden']");
                if (inputs == null)
                    throw new Exception("Champs de formulaire manquants dans la page Google Drive.");

                var query = HttpUtility.ParseQueryString(string.Empty);
                foreach (var input in inputs)
                {
                    string name = input.GetAttributeValue("name", "");
                    string value = input.GetAttributeValue("value", "");
                    if (!string.IsNullOrEmpty(name))
                        query[name] = value;
                }

                string downloadUrl = $"{actionUrl}?{query}";

                // Étape 3 : Télécharger le fichier réel
                using var response = await Client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
        }
}
