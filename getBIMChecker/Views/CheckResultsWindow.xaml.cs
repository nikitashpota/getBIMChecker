using System.Windows;

namespace getBIMChecker.Views
{
    /// <summary>
    /// Логика взаимодействия для CheckResultsWindow.xaml
    /// Немодальное окно для просмотра и исправления ошибок осей
    /// </summary>
    public partial class CheckResultsWindow : Window
    {
        public CheckResultsWindow()
        {
            InitializeComponent();

            // Добавляем конвертер BoolToVisibility в ресурсы
            if (!Resources.Contains("BoolToVisibilityConverter"))
            {
                Resources.Add("BoolToVisibilityConverter", new Converters.BoolToVisibilityConverter());
            }
        }
    }
}
