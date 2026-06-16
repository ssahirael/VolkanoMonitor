namespace VolcanoMonitor;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        UpdateSidebarUser();

        // Update sidebar when preferences change (after login)
        Navigated += (_, _) => UpdateSidebarUser();
    }

    private void UpdateSidebarUser()
    {
        var name = Preferences.Get("session_nama", "Pengguna");
        var role = Preferences.Get("session_role", "user");
        LblSidebarUserName.Text = name.Length > 14 ? name[..14] + "…" : name;
        LblSidebarUserRole.Text = role.ToUpper();
        LblAvatarInitial.Text = name.Length > 0 ? name[0].ToString().ToUpper() : "U";
    }
}