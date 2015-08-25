using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Wiggum
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        static readonly string TARGETPROCESS_BASE_URI = "https://targetprocess.smarttech.com";
        static readonly string SELECTEDPROJECTID_SETTINGS_KEY = "SelectedProjectId";

        HttpClient client;

        public MainPage()
        {
            this.InitializeComponent();
        }

        void resetUserNamePasswordField()
        {
            userNameField.IsEnabled = true;
            passwordField.IsEnabled = true;
            userNameField.SelectAll();
            userNameField.Focus(FocusState.Keyboard);
        }

        private void signInButton_Click(object sender, RoutedEventArgs e)
        {
            userNameField.IsEnabled = false;
            passwordField.IsEnabled = false;
            progressRing.IsActive = true;
            progressRing.Visibility = Visibility.Visible;
            client = new HttpClient(new HttpBaseProtocolFilter { ServerCredential = new PasswordCredential(TARGETPROCESS_BASE_URI, userNameField.Text, passwordField.Password) });
            getContext();
        }

        static string ToSentenceCase(string s)
        {
            StringBuilder buf = new StringBuilder();
            for(int i = 0; i < s.Length; ++i)
            {
                if(i==0)
                {
                    buf.Append(char.ToUpperInvariant(s[i]));
                } else { buf.Append(s[i]); }
            }
            return buf.ToString();
        }
        async void getContext()
        {
            var req = await client.GetAsync(new Uri(string.Format("{0}/api/v1/Context?", TARGETPROCESS_BASE_URI)));
            progressRing.IsActive = false;
            progressRing.Visibility = Visibility.Collapsed;
            if (req.IsSuccessStatusCode)
            {
                string responseText = await req.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(responseText);
                int? selectedProjectId = null;
                ComboBoxItem firstComboBoxItem = null;
                bool selectedProjectFound = false;
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (settings.Values.ContainsKey(SELECTEDPROJECTID_SETTINGS_KEY))
                {
                    selectedProjectId = (int) settings.Values[SELECTEDPROJECTID_SETTINGS_KEY];
                }
                var context = doc.Root;
                var selectedProjects = context.Element("SelectedProjects");
                var projectInfo = selectedProjects.Elements("ProjectInfo");
                foreach (XElement e in projectInfo)
                {
                    ComboBoxItem i = new ComboBoxItem();
                    i.Content = e.Attribute("Name").Value;
                    int id = int.Parse(e.Attribute("Id").Value);
                    i.Tag = id;
                    if(firstComboBoxItem == null)
                    {
                        firstComboBoxItem = i;
                    }
                    if(!selectedProjectId.HasValue)
                    {
                        selectedProjectId = id;
                        settings.Values[SELECTEDPROJECTID_SETTINGS_KEY] = id;
                    }
                    if(id == selectedProjectId.Value)
                    {
                        selectedProjectFound = true;
                        i.IsSelected = true;
                    }
                    projectComboBox.Items.Add(i);
                }
                if(!selectedProjectFound)
                {
                    firstComboBoxItem.IsSelected = true;
                }
                projectLabel.Visibility = Visibility.Visible;
                projectComboBox.Visibility = Visibility.Visible;
                client.Dispose();
                resetUserNamePasswordField();
            }
            else if(req.StatusCode == HttpStatusCode.Unauthorized || req.StatusCode == HttpStatusCode.Forbidden)
            {
                var popup = new MessageDialog("Check username/password and try again.", "Access denied");
                await popup.ShowAsync();
                client.Dispose();
                resetUserNamePasswordField();
            }
            else
            {
                var popup = new MessageDialog("Check network settings and try again.", string.IsNullOrWhiteSpace(req.ReasonPhrase) ? "Network error" : ToSentenceCase(req.ReasonPhrase));
                await popup.ShowAsync();
                client.Dispose();
                resetUserNamePasswordField();
            }
        }

        private void userNamePasswordField_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                if (string.IsNullOrWhiteSpace(userNameField.Text))
                {
                    userNameField.Focus(FocusState.Keyboard);
                }
                else if (string.IsNullOrWhiteSpace(passwordField.Password))
                {
                    passwordField.Focus(FocusState.Keyboard);
                }
                else
                {
                    signInButton.Focus(FocusState.Keyboard);
                    (new ButtonAutomationPeer(signInButton).GetPattern(PatternInterface.Invoke) as IInvokeProvider).Invoke();
                }
            }
        }
    }
}
