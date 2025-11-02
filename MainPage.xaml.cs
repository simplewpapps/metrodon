//using System...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
//using Windows...
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Data.Json;
using Windows.UI.Popups;
using Windows.Web.Http;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;


namespace MastodonPlusPlus
{
    public sealed partial class MainPage : Page
    {
        public ObservableCollection<Post> Posts { get; set; }
        public ObservableCollection<Hashtag> Hashtags { get; set; } = new ObservableCollection<Hashtag>();

        public MainPage()
        {
            this.InitializeComponent();
            this.DataContext = this;
            Hashtags = new ObservableCollection<Hashtag>();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            Posts = new ObservableCollection<Post>();
            TimelineListView.ItemsSource = Posts;
            LoadPublicTimelineAsync();
            LoadHashtagsAsync();
            
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SignInPage));
        }

        private async void LoadPublicTimelineAsync()
        {
            try
            {
                Uri url = new Uri($"https://{Settings.Domain}/api/v1/timelines/public?limit=500");
                HttpClient client = new HttpClient();
                string response = await client.GetStringAsync(url);
                ParseTimeline(response);
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog("Error loading feed:\n" + ex.Message);
                await dialog.ShowAsync();
            }
        }

        private async void LoadHashtagsAsync()
        {
            try
            {
                Uri url = new Uri($"https://{Settings.Domain}/api/v1/trends/tags?limit=500");
                HttpClient client = new HttpClient();
                string response = await client.GetStringAsync(url);
                ParseHashtags(response);
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog("Error loading hastags:\n" + ex.Message);
                await dialog.ShowAsync();
            }
        }

        private void ParseHashtags(string json)
        {
            JsonArray hashtagsArray = JsonArray.Parse(json);

            foreach (var item in hashtagsArray)
            {
                JsonObject tagObj = item.GetObject();
                if (tagObj.ContainsKey("name"))
                {
                    string tagName = tagObj.GetNamedString("name", string.Empty);
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        Hashtags.Add(new Hashtag { Name = "#" + tagName });
                    }
                }

            }

        }


        private string ExtractParagraph(string htmlContent)
        {
            var match = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<p>(.*?)</p>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            string paragraph = match.Success ? match.Groups[1].Value : htmlContent;

            paragraph = System.Text.RegularExpressions.Regex.Replace(paragraph,
                @"<a\s+href=[""'](.*?)[""'].*?>(.*?)</a>",
                "$1",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            paragraph = System.Text.RegularExpressions.Regex.Replace(paragraph, @"<.*?>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return paragraph;
        }

        private string EscapeForXml(string text)
        {

            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var cleaned = text.Replace("\r", " ").Replace("\n", " ");
            if (cleaned.Length > 100)
                cleaned = cleaned.Substring(0, 100) + "...";
            return text
                .Replace("&", "&amp")
                .Replace("<", "&lt")
                .Replace(">", "&gt")
                .Replace("\"", "&quot")
                .Replace("'", "&apos");

            
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("Metrodon is unofficial Mastodon instances reader for Windows Phone. \nThis app has strict NSFW filter, but some sensitive content still may be shown. That is not my fault. \nCredits to Mastodon instances! \nVisit: https://joinmastodon.org \n \nCreated by Simple WP Apps!", "About");
            await dialog.ShowAsync();
        }

        

        private void ParseTimeline(string json)
        {
            var posts = new List<Post>();
            JsonArray postsArray = JsonArray.Parse(json);

            var bannedWords = new List<string> { "nsfw", "NSFW", "18+", "Sensitive", "sensitive", "minds.com", "porn", "porno", "nude", "nudity", "nude", "pussy", "tits", "boobs", "dick", "cock", "penis" };

            foreach (var item in postsArray)
            {
                JsonObject postObj = item.GetObject();
                string rawContent = postObj["content"].GetString();
                string content = ExtractParagraph(rawContent);
                string account = postObj["account"].GetObject()["username"].GetString();

                bool containsBanned = bannedWords.Any(word => content.Contains(word));
                if (containsBanned) continue;

                

                List<string> imageUrls = new List<string>();
                JsonArray mediaArray = postObj.GetNamedArray("media_attachments", new JsonArray());
                foreach (var mediaItem in mediaArray)
                {
                    JsonObject mediaObj = mediaItem.GetObject();
                    string type = mediaObj.GetNamedString("type", "");
                    if (type == "image")
                    {
                        string previewUrl = mediaObj.GetNamedString("preview_url", "");
                        imageUrls.Add(previewUrl);
                    }
                }

                bool isSensitive = postObj.GetNamedBoolean("sensitive", false);
                string spoilerText = postObj.GetNamedString("spoiler_text", "");
                if (isSensitive)
                {
                    content = "WARNING: NSFW 18+ " + (string.IsNullOrEmpty(spoilerText) ? "Sensitive content" : spoilerText);
                    imageUrls.Clear();
                }


                Posts.Add(new Post
                {
                    Account = account,
                    Content = content,
                    ImageUrls = imageUrls
                });

                //ShowPostsDebug(tilePosts);
            }

        }

        private async Task LoadTimelineAsync()
        {
            var http = new Windows.Web.Http.HttpClient();
            var response = await http.GetStringAsync(new Uri($"https://{Settings.Domain}/api/v1/timelines/public?limit=500"));

            ParseTimeline(response);
        }



        private async void ChangeInstance_Click(object sender, RoutedEventArgs e)
        {
            var inputBox = new TextBox
            {
                PlaceholderText = "Enter an instance (example: mas.to)",
                Text = Settings.Domain
            };

            var dialog = new ContentDialog
            {
                Title = "Choose Mastodon instance",
                Content = inputBox,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel"
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string enteredInstance = inputBox.Text.Trim();
                if (!string.IsNullOrEmpty(enteredInstance))
                {
                    Settings.Domain = enteredInstance;

                    LoadPublicTimelineAsync();
                    LoadHashtagsAsync();
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await LoadTimelineAsync();
        }
    }
    public class Post
    {
        public string Account { get; set; }
        public string Content { get; set; }
        public List<string> ImageUrls { get; set; } = new List<string>();
    }
    public class Hashtag
    {
        public string Name { get; set; }
    }

    public static class Settings
    {
        private const string InstanceKey = "InstanceDomain";

        public static string Domain
        {
            get
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey(InstanceKey))
                    return (string)localSettings.Values[InstanceKey];
                return "mas.to"; //by default
                
            }
            set
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values[InstanceKey] = value;
            }
        }
    }
}
