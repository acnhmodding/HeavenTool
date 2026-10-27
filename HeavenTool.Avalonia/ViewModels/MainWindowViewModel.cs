using Avalonia.Controls;
using GamebananaAPI;
using HeavenTool.Avalonia.Views.FileEditors;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<ModItem> ModItems { get; } = [];

        public bool _isPaneOpen = true;
        
        public MainWindowViewModel()
        {
            if (Design.IsDesignMode)
                return;

            var gamebananaAPI = new GamebananaClient();
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var feed = await gamebananaAPI.GetSubfeed(7923, category: Category.Mod);
                    if (feed == null || feed.Records == null) return;

                    foreach (var mod in feed.Records)
                    {
                        if (mod == null) continue;

                        var imageData = mod?.PreviewImage?.Data?[0];
                        string imageUrl = null;
                        if (imageData != null && imageData.File != null)
                            imageUrl = $"{imageData.BaseUrl}/{imageData.File}";

                        ModItems.Add(new ModItem
                        {
                            Name = mod.Name,
                            Description = $"by {mod.Submitter.Name}",
                            ImagePath = imageUrl ?? ""
                        });
                    }
                }
                catch
                {
                }
            });

            
        }

        public void InstallMod()
        {

        }

        public void OpenBCSV()
        {
            var dialog = new BCSVWindow();
            //dialog.DataContext = interaction.Input;

            dialog.Show();
        }

        public void OpenBFRES() => new BFRESWindow().Show();

    }
}
