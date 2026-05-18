using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;
using TranslatorLibrary;

namespace MisakaTranslator_WPF.SettingsPages.TranslatorPages
{
    /// <summary>
    /// CustomOpenAITransSettingsPage.xaml 的交互逻辑
    /// </summary>
    public partial class CustomOpenAITransSettingsPage : Page
    {
        private bool isInitialized;

        public CustomOpenAITransSettingsPage()
        {
            InitializeComponent();

            CustomOpenAIBaseUrlBox.Text = Common.appSettings.CustomOpenAIBaseUrl;
            CustomOpenAIApiKeyBox.Text = Common.appSettings.CustomOpenAIApiKey;
            CustomOpenAIModelBox.Text = Common.appSettings.CustomOpenAIModelName;
            if (!string.IsNullOrWhiteSpace(Common.appSettings.CustomOpenAIModelName))
            {
                CustomOpenAIModelBox.ItemsSource = new List<string> { Common.appSettings.CustomOpenAIModelName };
            }

            CustomOpenAIPromptTemplateBox.Text = Common.appSettings.CustomOpenAIPromptTemplate;
            CustomOpenAITemperatureBox.Value = Common.appSettings.CustomOpenAITemperature;
            CustomOpenAITemperatureBox.ValueChanged += CustomOpenAITemperatureBox_ValueChanged;

            isInitialized = true;
        }

        private void CustomOpenAISetting_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveCustomOpenAISettings(false);
        }

        private void CustomOpenAIModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized)
            {
                return;
            }

            if (CustomOpenAIModelBox.SelectedItem != null)
            {
                CustomOpenAIModelBox.Text = CustomOpenAIModelBox.SelectedItem.ToString();
            }

            SaveCustomOpenAISettings(false);
        }

        private void CustomOpenAITemperatureBox_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            SaveCustomOpenAISettings(false);
        }

        private async void CustomOpenAIGetModelsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCustomOpenAISettings(true);
            CustomOpenAIGetModelsButton.IsEnabled = false;

            try
            {
                var result = await CustomOpenAITranslator.GetModelsAsync(
                    Common.appSettings.CustomOpenAIBaseUrl,
                    Common.appSettings.CustomOpenAIApiKey);

                if (!result.Success)
                {
                    Growl.Error($"{Application.Current.Resources["CustomOpenAITransSettingsPage_GetModelsError"]}\n{result.Error}");
                    return;
                }

                CustomOpenAIModelBox.ItemsSource = result.Models;
                var currentModel = Common.appSettings.CustomOpenAIModelName;
                if (!string.IsNullOrWhiteSpace(currentModel) && result.Models.Contains(currentModel))
                {
                    CustomOpenAIModelBox.SelectedItem = currentModel;
                }
                else
                {
                    CustomOpenAIModelBox.SelectedItem = result.Models[0];
                    Common.appSettings.CustomOpenAIModelName = result.Models[0];
                }

                Growl.Success($"{Application.Current.Resources["CustomOpenAITransSettingsPage_GetModelsSuccess"]}: {result.Models.Count}");
            }
            finally
            {
                CustomOpenAIGetModelsButton.IsEnabled = true;
            }
        }

        private async void CustomOpenAITestButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCustomOpenAISettings(true);
            CustomOpenAITestButton.IsEnabled = false;

            try
            {
                var trans = new CustomOpenAITranslator();
                trans.Configure(
                    Common.appSettings.CustomOpenAIApiKey,
                    Common.appSettings.CustomOpenAIBaseUrl,
                    Common.appSettings.CustomOpenAIModelName,
                    Common.appSettings.CustomOpenAIPromptTemplate,
                    Common.appSettings.CustomOpenAITemperature);

                var res = await trans.TranslateAsync(TestSrcText.Text, "zh", "jp");

                if (res != null)
                {
                    HandyControl.Controls.MessageBox.Show(res, Application.Current.Resources["MessageBox_Result"].ToString());
                }
                else
                {
                    Growl.Error($"{Application.Current.Resources["CustomOpenAITransSettingsPage_TestError"]}\n{trans.GetLastError()}");
                }
            }
            finally
            {
                CustomOpenAITestButton.IsEnabled = true;
            }
        }

        private void SaveCustomOpenAISettings(bool showPromptPlaceholderHint)
        {
            Common.appSettings.CustomOpenAIBaseUrl = CustomOpenAIBaseUrlBox.Text.Trim();
            Common.appSettings.CustomOpenAIApiKey = CustomOpenAIApiKeyBox.Text.Trim();
            Common.appSettings.CustomOpenAIModelName = GetCustomOpenAIModelName();
            Common.appSettings.CustomOpenAIPromptTemplate = CustomOpenAIPromptTemplateBox.Text;
            Common.appSettings.CustomOpenAITemperature = CustomOpenAITemperatureBox.Value;

            if (showPromptPlaceholderHint
                && !string.IsNullOrWhiteSpace(Common.appSettings.CustomOpenAIPromptTemplate)
                && !Common.appSettings.CustomOpenAIPromptTemplate.Contains(CustomOpenAITranslator.TextPlaceholder))
            {
                Growl.Info(Application.Current.Resources["CustomOpenAITransSettingsPage_PlaceholderHint"].ToString());
            }
        }

        private string GetCustomOpenAIModelName()
        {
            if (CustomOpenAIModelBox.SelectedItem != null)
            {
                return CustomOpenAIModelBox.SelectedItem.ToString().Trim();
            }

            return CustomOpenAIModelBox.Text.Trim();
        }
    }
}
