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
using Windows.Storage;
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
using Windows.UI.Xaml.Shapes;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using WinRTXamlToolkit.Controls.DataVisualization.Charting;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Wiggum
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        static readonly string TARGETPROCESSSERVER_SETTINGSKEY = "TargetProcessServer";
        static readonly string DEFAULT_TARGETPROCESSSERVER = "https://targetprocess.smarttech.com";
        static readonly string SELECTEDPROJECTID_SETTINGSKEY = "SelectedProjectId";
        static readonly int DEFAULT_SELECTEDPROJECTID = 312391;
        static readonly string INCLUDE = "[Id,Name,Tags,EndDate,UserStory[Name,Feature]]";

        HttpClient client;
        string targetProcessServer;
        int selectedProjectId;
        private ApplicationDataContainer settings;

        public MainPage()
        {
            this.InitializeComponent();
            settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey(TARGETPROCESSSERVER_SETTINGSKEY)) { targetProcessServer = (string)settings.Values[TARGETPROCESSSERVER_SETTINGSKEY]; } else { targetProcessServer = DEFAULT_TARGETPROCESSSERVER; settings.Values[TARGETPROCESSSERVER_SETTINGSKEY] = targetProcessServer; }
            if (settings.Values.ContainsKey(SELECTEDPROJECTID_SETTINGSKEY)) { selectedProjectId = (int)settings.Values[SELECTEDPROJECTID_SETTINGSKEY]; } else { selectedProjectId = DEFAULT_SELECTEDPROJECTID; settings.Values[SELECTEDPROJECTID_SETTINGSKEY] = selectedProjectId; }
        }

        void resetUserNamePasswordField()
        {
            progressRing.IsActive = false;
            userNameLabel.Visibility = Visibility.Visible;
            userNameField.Visibility = Visibility.Visible;
            passwordField.Visibility = Visibility.Visible;
            passwordLabel.Visibility = Visibility.Visible;
            projectLabel.Visibility = Visibility.Collapsed;
            projectComboBox.Visibility = Visibility.Collapsed;
            userNameField.IsEnabled = true;
            passwordField.IsEnabled = true;
            signInButton.IsEnabled = false;
            passwordField.Password = "";
            userNameField.SelectAll();
            userNameField.Focus(FocusState.Keyboard);
        }

        private void signInButton_Click(object sender, RoutedEventArgs e)
        {
            if (features == null || features.Count == 0)
            {
                userNameField.IsEnabled = false;
                passwordField.IsEnabled = false;
                signInButton.IsEnabled = false;
                progressRing.IsActive = true;
                client = new HttpClient(new HttpBaseProtocolFilter { ServerCredential = new PasswordCredential(targetProcessServer, userNameField.Text, passwordField.Password) });
                getContext();
            }
            else
            {
                signInButton.Content = "Sign in";
                resetUserNamePasswordField();
                scoreboard.Visibility = Visibility.Collapsed;
                networkChart.Series.Clear();
                idfChart.Series.Clear();
                features.Clear();
            }
        }

        static string ToSentenceCase(string s)
        {
            StringBuilder buf = new StringBuilder();
            for (int i = 0; i < s.Length; ++i)
            {
                if (i == 0)
                {
                    buf.Append(char.ToUpperInvariant(s[i]));
                }
                else { buf.Append(s[i]); }
            }
            return buf.ToString();
        }

        class ChartDatum
        {
            public int week
            {
                get; set;
            }
            public int count
            {
                get; set;
            }
            public string title { get; set; }
            public string details { get; set; }
            public SolidColorBrush fill { get; set; }
            public SolidColorBrush stroke { get; set; }

            public int strokeWidth { get; set; }
        }

        class Feature
        {
            public int id;
            public string name;
            public List<Task> networks = new List<Task>();
            public List<Task> idfs = new List<Task>();
            public ChartDatum[] networkdata;
            public ChartDatum[] idfdata;

            public void summarize(int weeks, SolidColorBrush fill, SolidColorBrush stroke1, SolidColorBrush stroke2)
            {
                networkdata = new ChartDatum[weeks];
                idfdata = new ChartDatum[weeks];
                for (int i = 0; i < weeks; ++i)
                {
                    ChartDatum d = new ChartDatum();
                    d.title = name;
                    d.week = i;
                    d.fill = fill;
                    d.stroke = stroke1;
                    d.strokeWidth = 1;
                    networkdata[i] = d;
                    d = new ChartDatum();
                    d.title = name;
                    d.week = i;
                    d.fill = fill;
                    d.stroke = stroke1;
                    d.strokeWidth = 1;
                    idfdata[i] = d;
                }
                foreach (Task t in networks)
                {
                    ChartDatum d = networkdata[t.week];
                    d.count = d.count + 1;
                    if (!string.IsNullOrWhiteSpace(d.details)) { d.details = d.details + "\r\n" + t.name; } else { d.details = t.name; }
                    d.stroke = stroke2;
                    d.strokeWidth = 2;
                }
                foreach (Task t in idfs)
                {
                    ChartDatum d = idfdata[t.week];
                    d.count = d.count + 1;
                    if (!string.IsNullOrWhiteSpace(d.details)) { d.details = d.details + "\r\n" + t.name; } else { d.details = t.name; }
                    d.stroke = stroke2;
                    d.strokeWidth = 2;
                }
                for (int i = 0; i < weeks; ++i)
                {
                    ChartDatum d = networkdata[i];
                    if (string.IsNullOrWhiteSpace(d.details)) { d.details = "No change this week"; }
                    d = idfdata[i];
                    if (string.IsNullOrWhiteSpace(d.details)) { d.details = "No change this week"; }
                }
                for (int i = 1; i < weeks; ++i)
                {
                    ChartDatum d = networkdata[i];
                    ChartDatum c = networkdata[i - 1];
                    d.count = d.count + c.count;
                    d = idfdata[i];
                    c = idfdata[i - 1];
                    d.count = d.count + c.count;
                }
            }
        }

        Dictionary<int, Feature> features = new Dictionary<int, Feature>();
        List<Task> networks = new List<Task>();
        List<Task> idfs = new List<Task>();
        private bool gettingContext;

        class Task
        {
            public int id;
            public string name;
            public DateTime enddate;
            public int feature;
            public int week;
        }

        async void getTasks(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                uri = string.Format("{0}/api/v1/Tasks?format=xml&include={1}&where={2}", targetProcessServer, Uri.EscapeDataString(INCLUDE), Uri.EscapeDataString(String.Format("Project.ID eq {0}", selectedProjectId)));
            }
            progressRing.IsActive = true;
            var req = await client.GetAsync(new Uri(uri));
            if (req.IsSuccessStatusCode)
            {
                DateTime start = new DateTime(2015, 04, 1);
                while (start.DayOfWeek > DayOfWeek.Monday)
                {
                    start = start.AddDays(-1);
                }
                string responseText = await req.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(responseText);
                req.Dispose();
                foreach (XElement Task in doc.Root.Elements("Task"))
                {
                    XElement Tags = Task.Element("Tags");
                    string tags = Tags != null ? Tags.Value.ToLowerInvariant() : null;
                    XElement EndDate = Task.Element("EndDate");
                    XElement UserStory = Task.Element("UserStory");
                    XElement Feature = UserStory != null ? UserStory.Element("Feature") : null;
                    if (!string.IsNullOrWhiteSpace(tags) && EndDate != null && !string.IsNullOrWhiteSpace(EndDate.Value) && UserStory != null && Feature != null)
                    {
                        Task t = new Task();
                        t.id = int.Parse(Task.Attribute("Id").Value);
                        t.name = Task.Attribute("Name").Value;
                        t.enddate = DateTime.Parse(EndDate.Value);
                        t.week = (t.enddate - start).Days / 7;
                        if (t.week < 0)
                        {
                            t.week = 0;
                        }
                        t.feature = int.Parse(Feature.Attribute("Id").Value);
                        Feature f;
                        if (features.ContainsKey(t.feature))
                        {
                            f = features[t.feature];
                        }
                        else
                        {
                            f = new Feature();
                            f.id = t.feature;
                            f.name = Feature.Attribute("Name").Value;
                            features[f.id] = f;
                        }
                        if (tags.Contains("network"))
                        {
                            f.networks.Add(t);
                            networks.Add(t);
                        }
                        if (tags.Contains("idf"))
                        {
                            f.idfs.Add(t);
                            idfs.Add(t);
                        }
                    }
                }
                XAttribute Next = doc.Root.Attribute("Next");
                if (Next != null && !string.IsNullOrWhiteSpace(Next.Value))
                {
                    getTasks(Next.Value);
                }
                else
                {
                    showData();
                }
            }
            else
            {
                progressRing.IsActive = false;
                var popup = new MessageDialog("Check username/password and try again.", "Access denied");
                req.Dispose();
                await popup.ShowAsync();
                client.Dispose();
                resetUserNamePasswordField();
            }
        }

        async void getContext()
        {
            var req = await client.GetAsync(new Uri(string.Format("{0}/api/v1/Context?", targetProcessServer)));
            if (req.IsSuccessStatusCode)
            {
                string responseText = await req.Content.ReadAsStringAsync();
                req.Dispose();
                var doc = XDocument.Parse(responseText);
                ComboBoxItem firstComboBoxItem = null;
                bool selectedProjectFound = false;
                gettingContext = true;
                foreach (XElement e in doc.Root.Element("SelectedProjects").Elements("ProjectInfo"))
                {
                    ComboBoxItem i = new ComboBoxItem();
                    i.Content = e.Attribute("Name").Value;
                    int id = int.Parse(e.Attribute("Id").Value);
                    i.Tag = id;
                    if (firstComboBoxItem == null)
                    {
                        firstComboBoxItem = i;
                    }
                    if (id == selectedProjectId)
                    {
                        selectedProjectFound = true;
                        i.IsSelected = true;
                    }
                    projectComboBox.Items.Add(i);
                }
                if (!selectedProjectFound)
                {
                    firstComboBoxItem.IsSelected = true;
                    selectedProjectId = (int)firstComboBoxItem.Tag;
                    settings.
                        Values[SELECTEDPROJECTID_SETTINGSKEY] = selectedProjectId;
                }
                gettingContext = false;
                projectLabel.Visibility = Visibility.Visible;
                projectComboBox.Visibility = Visibility.Visible;
                getTasks(null);
            }
            else if (req.StatusCode == HttpStatusCode.Unauthorized || req.StatusCode == HttpStatusCode.Forbidden)
            {
                var popup = new MessageDialog("Check username/password and try again.", "Access denied");
                req.Dispose();
                await popup.ShowAsync();
                client.Dispose();
                resetUserNamePasswordField();
            }
            else
            {
                var popup = new MessageDialog("Check network settings and try again.", string.IsNullOrWhiteSpace(req.ReasonPhrase) ? "Network error" : ToSentenceCase(req.ReasonPhrase));
                req.Dispose();
                await popup.ShowAsync();
                client.Dispose();
                resetUserNamePasswordField();
            }
        }

        private void userNamePasswordField_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            signInButton.IsEnabled = !string.IsNullOrWhiteSpace(userNameField.Text);
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

        private void projectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!gettingContext)
            {
                selectedProjectId = (int)(e.AddedItems.First() as ComboBoxItem).Tag;
                settings.Values[SELECTEDPROJECTID_SETTINGSKEY] = selectedProjectId;
                getTasks(null);
            }
        }

        void showData()
        {
            int weeks = 0;
            foreach (Task t in networks)
            {
                weeks = Math.Max(weeks, t.week);
            }
            foreach (Task t in idfs)
            {
                weeks = Math.Max(weeks, t.week);
            }
            ++weeks;
            networkChart.Series.Clear();
            idfChart.Series.Clear();
            int i = 0;
            string[] palette = new string[] { "Primary", "Secondary", "Tertiary", "Quartenary", "Quintenary", "Senary", "Septenary", "Octonary", "Nonary" };
            foreach (Feature f in features.Values)
            {
                string color = palette[i % palette.Length];
                SolidColorBrush fill = App.Current.Resources[string.Format("Theme{0}ColorBrush", color)] as SolidColorBrush;
                SolidColorBrush stroke1 = App.Current.Resources[string.Format("Theme{0}LighterColorBrush", color)] as SolidColorBrush;
                SolidColorBrush stroke2 = App.Current.Resources[string.Format("Theme{0}DarkestColorBrush", color)] as SolidColorBrush;

                f.summarize(weeks, fill, stroke1, stroke2);
                if (f.networks.Count > 0)
                {
                    LineSeries s = getLineSeries(f, fill);
                    s.ItemsSource = f.networkdata;
                    int m = f.networkdata[weeks - 2].count, n = f.networkdata[weeks - 1].count;
                    if (n > m)
                    {
                        s.Title = string.Format("{0} ({1}, \u0394{2})", f.name, n, n - m);
                    }
                    else
                    {
                        s.Title = string.Format("{0} ({1})", f.name, n);
                    }
                    networkChart.Series.Add(s);
                }
                if (f.idfs.Count > 0)
                {
                    LineSeries s = getLineSeries(f, fill);
                    s.ItemsSource = f.idfdata;
                    int m = f.idfdata[weeks - 2].count, n = f.idfdata[weeks - 1].count;
                    if (n > m)
                    {
                        s.Title = string.Format("{0} ({1}, \u0394{2})", f.name, n, n - m);
                    }
                    else
                    {
                        s.Title = string.Format("{0} ({1})", f.name, n);
                    }
                    idfChart.Series.Add(s);
                }
                if (f.networks.Count + f.idfs.Count > 0)
                {
                    ++i;
                }
            }
            scoreboard.Visibility = Visibility.Visible;
            progressRing.IsActive = false;
            signInButton.Content = "Sign out";
            signInButton.IsEnabled = true;
        }
        private LineSeries getLineSeries(Feature f, SolidColorBrush fill)
        {
            LineSeries s = new LineSeries();
            Style n = new Style(typeof(LineDataPoint));
            n.BasedOn = App.Current.Resources["ThemeLineSeriesDataPointStyle"] as Style;
            n.Setters.Add(new Setter(LineDataPoint.BackgroundProperty, fill));
            s.DataPointStyle = n;
            s.IndependentValuePath = "week";
            s.DependentValuePath = "count";
            return s;
        }
    }

}
