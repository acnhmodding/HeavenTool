using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.ViewModels;

namespace HeavenTool.Avalonia.Views
{
    public partial class MainWindow : FAAppWindow
    {

        public MainWindow()
        {
            InitializeComponent();

            var dataContext = new MainWindowViewModel();
            DataContext = dataContext;
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.Height = 32;
        }
    }
}
