using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Web;

namespace GameLauncher
{
    class MyWebClient : WebClient
    {
        public CookieContainer CookieContainer = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            request.CookieContainer = CookieContainer;
            return request;
        }
    }

    public static class GoogleDriveHelper
    {
        public static async Task DownloadFileAsync(string fileId, string destinationPath)
        {
            string baseUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

            using (var client = new MyWebClient())
            {
                // Étape 1️⃣ : Télécharger la page d'avertissement
                string htmlPage = await client.DownloadStringTaskAsync(baseUrl);

                // Étape 2️⃣ : Parser la page HTML
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlPage);

                // Chercher le formulaire avec l'id "download-form"
                var formNode = doc.DocumentNode.SelectSingleNode("//form[@id='download-form']");
                if (formNode == null)
                    throw new Exception("Formulaire de téléchargement introuvable sur la page Google Drive.");

                string actionUrl = formNode.GetAttributeValue("action", null);
                if (string.IsNullOrEmpty(actionUrl))
                    throw new Exception("Attribut 'action' manquant dans le formulaire de téléchargement.");

                // Récupérer tous les champs cachés (id, export, confirm, uuid)
                var inputs = formNode.SelectNodes(".//input[@type='hidden']");
                if (inputs == null)
                    throw new Exception("Champs de formulaire manquants dans la page Google Drive.");

                // Construire les paramètres GET
                var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
                foreach (var input in inputs)
                {
                    string name = input.GetAttributeValue("name", "");
                    string value = input.GetAttributeValue("value", "");
                    if (!string.IsNullOrEmpty(name))
                        query[name] = value;
                }

                // Étape 3️⃣ : Construire l’URL complète de téléchargement
                string downloadUrl = $"{actionUrl}?{query}";

                // Étape 4️⃣ : Télécharger le fichier
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);

                await client.DownloadFileTaskAsync(downloadUrl, destinationPath);
            }
        }
    }
}
