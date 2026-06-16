namespace VolcanoMonitor
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Preferences.Set("session_active", false);
        }

        // .NET 9 MAUI: gunakan CreateWindow bukan MainPage = new AppShell()
        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}