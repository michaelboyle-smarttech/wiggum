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
        static readonly string SELECTEDTEAMID_SETTINGSKEY = "SelectedTeamId";
        static readonly int DEFAULT_SELECTEDPROJECTID = 312391;
        static readonly int DEFAULT_SELECTED_TEAMID = 312747;
        static readonly string TASK_INCLUDE = "[Id,Name,Tags,EntityState,StartDate,EndDate,UserStory[Name,Feature]]";
        static readonly string TASK_WHERE = "EntityState.IsFinal eq \'True\'";
        static readonly string USERSTORY_INCLUDE = "[Id,Name,CreateDate,StartDate,EndDate,CustomFields,Feature[Id,Name,CustomFields]]";
        static readonly string USERSTORY_WHERE = "CustomFields.WIG eq \'True\' or (Feature is not null and Feature.CustomFields.WIG eq \'True\'";
        HttpClient client;
        string targetProcessServer;
        int selectedProjectId;
        int selectedTeamId;
        private ApplicationDataContainer settings;

        public MainPage()
        {
            this.InitializeComponent();
            settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey(TARGETPROCESSSERVER_SETTINGSKEY)) { targetProcessServer = (string)settings.Values[TARGETPROCESSSERVER_SETTINGSKEY]; } else { targetProcessServer = DEFAULT_TARGETPROCESSSERVER; settings.Values[TARGETPROCESSSERVER_SETTINGSKEY] = targetProcessServer; }
            if (settings.Values.ContainsKey(SELECTEDPROJECTID_SETTINGSKEY)) { selectedProjectId = (int)settings.Values[SELECTEDPROJECTID_SETTINGSKEY]; } else { selectedProjectId = DEFAULT_SELECTEDPROJECTID; settings.Values[SELECTEDPROJECTID_SETTINGSKEY] = selectedProjectId; }
            if (settings.Values.ContainsKey(SELECTEDTEAMID_SETTINGSKEY)) { selectedTeamId = (int)settings.Values[SELECTEDTEAMID_SETTINGSKEY]; } else { selectedTeamId = DEFAULT_SELECTED_TEAMID; settings.Values[SELECTEDTEAMID_SETTINGSKEY] = selectedTeamId; }
            start = new DateTime(2015, 04, 1);
            while (start.DayOfWeek > DayOfWeek.Monday)
            {
                start = start.AddDays(-1);
            }
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
            teamLabel.Visibility = Visibility.Collapsed;
            teamComboBox.Visibility = Visibility.Collapsed;
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
            public string subtitle { get; set; }
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

            public void summarize(DateTime start, int weeks, SolidColorBrush fill, SolidColorBrush stroke1, SolidColorBrush stroke2)
            {
                networkdata = new ChartDatum[weeks];
                idfdata = new ChartDatum[weeks];
                for (int i = 0; i < weeks; ++i)
                {
                    DateTime ws = start.AddDays(7 * i);
                    DateTime we = ws.AddDays(6);
                    string st = string.Format("Week of {0:MMM d} - {1:MMM d}", ws, we);
                    ChartDatum d = new ChartDatum();
                    d.title = name;
                    d.subtitle = st;
                    d.week = i;
                    d.fill = fill;
                    d.stroke = stroke1;
                    d.strokeWidth = 1;
                    networkdata[i] = d;
                    d = new ChartDatum();
                    d.title = name;
                    d.subtitle = st;
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

            public override string ToString()
            {
                return string.Format("FEATURE #{0} {1}", id, name);
            }

            public override int GetHashCode()
            {
                return id;
            }

            public override bool Equals(object obj)
            {
                return obj != null && obj is Feature && id == (obj as Feature).id;
            }
        }

        Dictionary<int, Feature> features = new Dictionary<int, Feature>();
        List<Task> networks = new List<Task>();
        List<Task> idfs = new List<Task>();
        private bool gettingContext;
        private string acid;
        private DateTime start;

        class Task
        {
            public int id;
            public string name;
            public DateTime enddate;
            public int feature;
            public int week;
        }

        async void getAcid()
        {
            string uri = string.Format("{0}/api/v1/Context?projectIds={1}&teamIds={2}", targetProcessServer, selectedProjectId, selectedTeamId);
            progressRing.IsActive = true;
            var req = await client.GetAsync(new Uri(uri));
            if (req.IsSuccessStatusCode)
            {
                string responseText = await req.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(responseText);
                req.Dispose();
                acid = doc.Root.Attribute("Acid").Value;
                getTasks(null);
            }
            else
            {
                progressRing.IsActive = false;
                var popup = new MessageDialog("Check project/team membership and try again.", "Access denied");
                req.Dispose();
                await popup.ShowAsync();
                client.Dispose();
                resetUserNamePasswordField();
            }
        }

        async void getTasks(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                uri = string.Format("{0}/api/v1/Tasks?acid={1}&format=xml&include={2}&where={3}", targetProcessServer, acid, Uri.EscapeDataString(TASK_INCLUDE), Uri.EscapeDataString(TASK_WHERE));
            }
            progressRing.IsActive = true;
            var req = await client.GetAsync(new Uri(uri));
            if (req.IsSuccessStatusCode)
            {
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
                    getUserStories(null);
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

        class UserStory
        {
            public int id { get; set; }
            public string name { get; set; }
            public int featureId { get; set; }
            public string featureName { get; set; }
            public string title { get; set; }
            public int opened { get; set; }
            public DateTime createdate { get; set; }
            public int started { get; set; }
            public DateTime? startdate { get; set; }
            public int closed { get; set; }
            public DateTime? enddate { get; set; }

            public override string ToString()
            {
                return string.Format("USER STORY #{0} {1}", id, name);
            }

            public override int GetHashCode()
            {
                return id;
            }

            public override bool Equals(object obj)
            {
                return obj != null && obj is UserStory && id == (obj as UserStory).id;
            }

        }

        List<UserStory> userStories = new List<UserStory>();

        async void getUserStories(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                uri = string.Format("{0}/api/v1/UserStories?acid={1}&format=xml&include={2}&where={3}", targetProcessServer, acid, Uri.EscapeDataString(USERSTORY_INCLUDE), Uri.EscapeDataString(USERSTORY_WHERE));
            }
            var req = await client.GetAsync(new Uri(uri));
            if (req.IsSuccessStatusCode)
            {
                string responseText = await req.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(responseText);
                req.Dispose();
                foreach (XElement UserStory in doc.Root.Elements("UserStory"))
                {
                    XElement CreateDate = UserStory.Element("CreateDate");
                    XElement StartDate = UserStory.Element("StartDate");
                    XElement EndDate = UserStory.Element("EndDate");
                    XElement Feature = UserStory.Element("Feature");
                    UserStory u = new UserStory();
                    u.id = int.Parse(UserStory.Attribute("Id").Value);
                    u.name = UserStory.Attribute("Name").Value;
                    XAttribute FeatureId = Feature != null ? Feature.Attribute("Id") : null;
                    u.featureId = FeatureId != null ? int.Parse(Feature.Attribute("Id").Value) : 0;
                    if (features.ContainsKey(u.featureId))
                    {
                        u.featureName = features[u.featureId].name;
                    }
                    if (!string.IsNullOrWhiteSpace(u.featureName))
                    {
                        u.title = string.Format("{0} in {1}", u.name, u.featureName);
                    }
                    else
                    {
                        u.title = u.name;
                    }
                    u.createdate = DateTime.Parse(CreateDate.Value);
                    u.opened = Math.Max(0, (int)((u.createdate - start).Days / 7));
                    if (StartDate != null && !string.IsNullOrWhiteSpace(StartDate.Value))
                    {
                        u.startdate = DateTime.Parse(StartDate.Value);
                        u.started = Math.Max(u.opened, (int)((u.startdate.Value - start).Days / 7));
                        if (EndDate != null && !string.IsNullOrWhiteSpace(EndDate.Value))
                        {
                            u.enddate = DateTime.Parse(EndDate.Value);
                            u.closed = Math.Max(u.started, (int)((u.enddate.Value - start).Days / 7));
                        }
                        else
                        {
                            u.enddate = null;
                            u.closed = -1;
                        }
                    }
                    else
                    {
                        u.startdate = null;
                        u.started = -1;
                        u.enddate = null;
                        u.closed = -1;
                    }
                    userStories.Add(u);
                }
                XAttribute Next = doc.Root.Attribute("Next");
                if (Next != null && !string.IsNullOrWhiteSpace(Next.Value))
                {
                    getUserStories(Next.Value);
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
                bool selectedItemFound = false;
                gettingContext = true;
                projectComboBox.Items.Clear();
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
                        selectedItemFound = true;
                        i.IsSelected = true;
                    }
                    projectComboBox.Items.Add(i);
                }
                if (!selectedItemFound)
                {
                    firstComboBoxItem.IsSelected = true;
                    selectedProjectId = (int)firstComboBoxItem.Tag;
                    settings.
                        Values[SELECTEDPROJECTID_SETTINGSKEY] = selectedProjectId;
                }
                firstComboBoxItem = null;
                selectedItemFound = false;
                teamComboBox.Items.Clear();
                foreach (XElement e in doc.Root.Element("SelectedTeams").Elements("TeamInfo"))
                {
                    ComboBoxItem i = new ComboBoxItem();
                    i.Content = e.Attribute("Name").Value;
                    int id = int.Parse(e.Attribute("Id").Value);
                    i.Tag = id;
                    if (firstComboBoxItem == null)
                    {
                        firstComboBoxItem = i;
                    }
                    if (id == selectedTeamId)
                    {
                        selectedItemFound = true;
                        i.IsSelected = true;
                    }
                    teamComboBox.Items.Add(i);
                }
                if (!selectedItemFound)
                {
                    firstComboBoxItem.IsSelected = true;
                    selectedTeamId = (int)firstComboBoxItem.Tag;
                    settings.Values[SELECTEDTEAMID_SETTINGSKEY] = selectedTeamId;
                }
                gettingContext = false;
                projectLabel.Visibility = Visibility.Visible;
                projectComboBox.Visibility = Visibility.Visible;
                teamLabel.Visibility = Visibility.Visible;
                teamComboBox.Visibility = Visibility.Visible;
                getAcid();
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

        private void projectTeamComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!gettingContext)
            {
                if (sender == projectComboBox)
                {
                    selectedProjectId = (int)(e.AddedItems.First() as ComboBoxItem).Tag;
                    settings.Values[SELECTEDPROJECTID_SETTINGSKEY] = selectedProjectId;
                }
                else if (sender == teamComboBox)
                {
                    selectedTeamId = (int)(e.AddedItems.First() as ComboBoxItem).Tag;
                    settings.Values[SELECTEDTEAMID_SETTINGSKEY] = selectedTeamId;
                }
                getAcid();
            }
        }

        void buildTaskCharts(int weeks)
        {
            int palindx = 0;
            string[] palette = new string[] { "Primary", "Secondary", "Tertiary", "Quartenary", "Quintenary", "Senary", "Septenary", "Octonary", "Nonary" };
            List<Feature> sorted = new List<Feature>(features.Values);
            sorted.Sort((x, y) => y.networks.Count - x.networks.Count);
            foreach (Feature f in sorted)
            {
                string color = palette[palindx % palette.Length];
                SolidColorBrush fill = App.Current.Resources[string.Format("Theme{0}ColorBrush", color)] as SolidColorBrush;
                SolidColorBrush stroke1 = App.Current.Resources[string.Format("Theme{0}LighterColorBrush", color)] as SolidColorBrush;
                SolidColorBrush stroke2 = App.Current.Resources[string.Format("Theme{0}DarkestColorBrush", color)] as SolidColorBrush;
                f.summarize(start, weeks, fill, stroke1, stroke2);
                if (f.networks.Count > 0)
                {
                    LineSeries s = getLineSeries(formatLegend(f.name, f.networkdata[weeks - 1].count, f.networkdata[weeks - 2].count), fill);
                    s.ItemsSource = f.networkdata;
                    networkChart.Series.Add(s);
                }
                if (f.idfs.Count > 0)
                {
                    LineSeries s = getLineSeries(formatLegend(f.name, f.networkdata[weeks - 1].count, f.networkdata[weeks - 2].count), fill);
                    s.ItemsSource = f.idfdata;
                    idfChart.Series.Add(s);
                }
                if (f.networks.Count + f.idfs.Count > 0)
                {
                    ++palindx;
                }
            }
        }

        void buildUserStoryCharts(int weeks)
        {
            ChartDatum[] opened = new ChartDatum[weeks];
            ChartDatum[] started = new ChartDatum[weeks];
            ChartDatum[] closed = new ChartDatum[weeks];
            for (int i = 0; i < weeks; ++i)
            {
                DateTime ws = start.AddDays(7 * i);
                DateTime we = ws.AddDays(6);
                string st = string.Format("Week of {0:MMM d} - {1:MMM d}", ws, we);
                string color = "Primary";
                SolidColorBrush fill = App.Current.Resources[string.Format("Theme{0}ColorBrush", color)] as SolidColorBrush;
                SolidColorBrush stroke1 = App.Current.Resources[string.Format("Theme{0}LighterColorBrush", color)] as SolidColorBrush;
                ChartDatum d = new ChartDatum();
                d.title = "Opened";
                d.subtitle = st;
                d.week = i;
                d.fill = fill;
                d.stroke = stroke1;
                d.strokeWidth = 1;
                opened[i] = d;
                color = "Secondary";
                fill = App.Current.Resources[string.Format("Theme{0}ColorBrush", color)] as SolidColorBrush;
                stroke1 = App.Current.Resources[string.Format("Theme{0}LighterColorBrush", color)] as SolidColorBrush;
                d = new ChartDatum();
                d.title = "In Progress";
                d.subtitle = st;
                d.week = i;
                d.fill = fill;
                d.stroke = stroke1;
                d.strokeWidth = 1;
                started[i] = d;
                color = "Tertiary";
                fill = App.Current.Resources[string.Format("Theme{0}ColorBrush", color)] as SolidColorBrush;
                stroke1 = App.Current.Resources[string.Format("Theme{0}LighterColorBrush", color)] as SolidColorBrush;
                d = new ChartDatum();
                d.title = "Closed";
                d.subtitle = st;
                d.week = i;
                d.fill = fill;
                d.stroke = stroke1;
                d.strokeWidth = 1;
                closed[i] = d;
            }
            foreach (UserStory u in userStories)
            {
                ChartDatum d = opened[u.opened];
                string color = "Primary";
                SolidColorBrush stroke2 = App.Current.Resources[string.Format("Theme{0}DarkestColorBrush", color)] as SolidColorBrush;
                d.stroke = stroke2;
                d.strokeWidth = 2;
                d.count = d.count + 1;
                if (!string.IsNullOrWhiteSpace(d.details)) { d.details = d.details + "\r\n" + u.title; } else { d.details = u.title; }
                if (u.started >= u.opened)
                {
                    d = started[u.started];
                    color = "Secondary";
                    stroke2 = App.Current.Resources[string.Format("Theme{0}DarkestColorBrush", color)] as SolidColorBrush;
                    d.stroke = stroke2;
                    d.strokeWidth = 2;
                    d.count = d.count + 1;
                    if (!string.IsNullOrWhiteSpace(d.details)) { d.details = d.details + "\r\n" + u.title; } else { d.details = u.title; }
                    if (u.closed >= u.started)
                    {
                        d = closed[u.closed];
                        color = "Tertiary";
                        stroke2 = App.Current.Resources[string.Format("Theme{0}DarkestColorBrush", color)] as SolidColorBrush;
                        d.stroke = stroke2;
                        d.strokeWidth = 2;
                        d.count = d.count + 1;
                        if (!string.IsNullOrWhiteSpace(d.details)) { d.details = d.details + "\r\n" + u.title; } else { d.details = u.title; }
                    }
                }
            }
            for (int i = 0; i < weeks; ++i)
            {
                ChartDatum d = opened[i];
                if (string.IsNullOrWhiteSpace(d.details)) { d.details = "No change this week"; }
                d = started[i];
                if (string.IsNullOrWhiteSpace(d.details)) { d.details = "No change this week"; }
                d = closed[i];
                if (string.IsNullOrWhiteSpace(d.details)) { d.details = "No change this week"; }
            }
            for (int i = 1; i < weeks; ++i)
            {
                ChartDatum d = opened[i];
                ChartDatum c = opened[i - 1];
                ChartDatum b = started[i];
                d.count = d.count + c.count - b.count;
            }
            for (int i = 1; i < weeks; ++i)
            {
                ChartDatum d = started[i];
                ChartDatum c = started[i - 1];
                ChartDatum b = closed[i];
                d.count = d.count + c.count - b.count;
            }
            for (int i = 1; i < weeks; ++i)
            {
                ChartDatum d = closed[i];
                ChartDatum c = closed[i - 1];
                d.count = d.count + c.count;
            }
            LineSeries s = getLineSeries(formatLegend("Opened", opened[weeks - 1].count, opened[weeks - 2].count),  App.Current.Resources[string.Format("Theme{0}ColorBrush", "Primary")] as SolidColorBrush);
            s.ItemsSource = opened;
            pipelineChart.Series.Add(s);
            s = getLineSeries(formatLegend("In Progress", started[weeks - 1].count, started[weeks - 2].count), App.Current.Resources[string.Format("Theme{0}ColorBrush", "Secondary")] as SolidColorBrush);
            s.ItemsSource = started;
            pipelineChart.Series.Add(s);
            s = getLineSeries(formatLegend("Closed", closed[weeks - 1].count, closed[weeks - 2].count), App.Current.Resources[string.Format("Theme{0}ColorBrush", "Tertiary")] as SolidColorBrush);
            s.ItemsSource = closed;
            pipelineChart.Series.Add(s);
        }

        static string formatLegend(string title, int thisWeek, int lastWeek)
        {
            if(thisWeek > lastWeek)
            {
                return string.Format("{0} ({1}, \u2191{2})", title, thisWeek, thisWeek - lastWeek);
            }
            else if (thisWeek < lastWeek)
            {
                return string.Format("{0} ({1}, \u2193{2})", title, thisWeek, lastWeek - thisWeek);
            }
            else
            {
                return string.Format("{0} ({1})", title, thisWeek);
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
            foreach (UserStory u in userStories)
            {
                weeks = Math.Max(weeks, u.opened);
                weeks = Math.Max(weeks, u.started);
                weeks = Math.Max(weeks, u.closed);
            }
            ++weeks;
            networkChart.Series.Clear();
            idfChart.Series.Clear();
            pipelineChart.Series.Clear();
            buildTaskCharts(weeks);
            buildUserStoryCharts(weeks);
            scoreboard.Visibility = Visibility.Visible;
            userNameLabel.Visibility = Visibility.Collapsed;
            userNameField.Visibility = Visibility.Collapsed;
            passwordField.Visibility = Visibility.Collapsed;
            passwordLabel.Visibility = Visibility.Collapsed;
            progressRing.IsActive = false;
            signInButton.Content = "Sign out";
            signInButton.IsEnabled = true;
        }


        private LineSeries getLineSeries(String title, SolidColorBrush fill)
        {
            LineSeries s = new LineSeries();
            Style n = new Style(typeof(LineDataPoint));
            n.BasedOn = App.Current.Resources["ThemeLineSeriesDataPointStyle"] as Style;
            n.Setters.Add(new Setter(LineDataPoint.BackgroundProperty, fill));
            s.DataPointStyle = n;
            s.IndependentValuePath = "week";
            s.DependentValuePath = "count";
            s.Title = title;
            return s;
        }
    }

}
