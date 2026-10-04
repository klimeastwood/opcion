using System.Reflection;

namespace NalogParserWPF.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        public string Version
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version == null
                    ? "1.0.0"
                    : $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }
    }
}
