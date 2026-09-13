namespace PropertyManagement.ViewModels.Portal;

public class ProfileViewModel
{
    public ProfileInfoViewModel Info { get; set; } = new();
    public ChangePasswordViewModel Password { get; set; } = new();
}
