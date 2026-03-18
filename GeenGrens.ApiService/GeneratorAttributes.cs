namespace GeenGrens.ApiService;

[AttributeUsage(AttributeTargets.Class)]
public class GenerateCrudAttribute : Attribute
{
    public bool Admin { get; }

    public GenerateCrudAttribute(bool admin)
    {
        Admin = admin;
    }
}
