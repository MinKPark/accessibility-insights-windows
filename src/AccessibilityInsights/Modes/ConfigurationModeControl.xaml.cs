// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using AccessibilityInsights.CommonUxComponents.Dialogs;
using AccessibilityInsights.Extensions.Helpers;
using AccessibilityInsights.SetupLibrary;
using AccessibilityInsights.SharedUx.Controls.CustomControls;
using AccessibilityInsights.SharedUx.Dialogs;
using AccessibilityInsights.SharedUx.Settings;
using AccessibilityInsights.SharedUx.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace AccessibilityInsights.Modes
{
    /// <summary>
    /// Interaction logic for ConfigurationModeControl.xaml
    /// </summary>
    public partial class ConfigurationModeControl : UserControl
    {
        /// <summary>
        /// MainWindow to access shared methods
        /// </summary>
        static MainWindow MainWin
        {
            get
            {
                return (MainWindow)Application.Current.MainWindow;
            }
        }

        /// <summary>
        /// App configation
        /// </summary>
        public static ConfigurationModel Configuration
        {
            get
            {
                return ConfigurationManager.GetDefaultInstance()?.AppConfig;
            }
        }

        /// <summary>
        /// Layout configuration
        /// </summary>
        public static AppLayout CurrentLayout
        {
            get
            {
                return ConfigurationManager.GetDefaultInstance()?.AppLayout;
            }
        }

        /// <summary>
        /// Overriding LocalizedControlType
        /// </summary>
        /// <returns></returns>
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new CustomControlOverridingAutomationPeer(this, "pane");
        }

        /// <summary>
        /// Keeps a snapshot of the configuration as a baseline for config diff
        /// </summary>
        private ConfigurationModel configSnapshot = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public ConfigurationModeControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Show/Hide Save Button from each tab
        /// </summary>
        /// <param name="show"></param>
        private void ShowSaveButton(bool show)
        {
            this.btnOk.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Change save button state as appropriate
        /// </summary>
        void UpdateSaveButtonState()
        {
            if (IsConfigurationChanged())
            {
                this.btnOk.IsEnabled = true;
            }
            else
            {
                this.btnOk.IsEnabled = false;
            }
        }

        /// <summary>
        /// Check whether any configuration is changed. 
        /// </summary>
        /// <returns></returns>
        private bool IsConfigurationChanged()
        {
            return appSettingsCtrl.IsConfigurationChanged(Configuration) 
                || connectionCtrl.IsConfigurationChanged(Configuration);
        }

        /// <summary>
        /// Recreate window close button behavior
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainWin.HideConfigurationMode();
            MainWin.UpdateMainWindowUI();
        }

        /// <summary>
        /// Handles click on OK button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            bool issueReporterUpdated = this.connectionCtrl.UpdateConfigFromSelections(Configuration);
            this.appSettingsCtrl.UpdateConfigFromSelections(Configuration);

            IReadOnlyDictionary<string, object> diff = ConfigurationModel.Diff(this.configSnapshot, Configuration);
            if (diff.Count > 0)
            {
                MainWin.HandleConfigurationChanged(diff);
            }

            if (issueReporterUpdated)
            {
                MainWin.UpdateMainWindowConnectionFields();
            }

            if (await HandleReleaseSwitch().ConfigureAwait(false))
                return;

            Dispatcher.Invoke(() => MainWin.TransitionToSelectActionMode());
        }

        private async Task<bool> HandleReleaseSwitch()
        {
            var channelDialog = new ChangeChannelContainedDialog(appSettingsCtrl.SelectedReleaseChannel);

            if (appSettingsCtrl.SelectedReleaseChannel == Configuration.ReleaseChannel ||
                !await MainWin.ctrlDialogContainer.ShowDialog(channelDialog).ConfigureAwait(false))
            {
                return false;
            }

            // If the window is topmost, the UAC prompt from the version switcher will appear behind the main window.
            // To prevent this, save the previous topmost state, ensure that the main window is not topmost when the
            // UAC prompt will display, then restore the previous topmost state.
            bool previousTopmostSetting = Configuration.AlwaysOnTop;

            try
            {
                Dispatcher.Invoke(() =>
                {
                    previousTopmostSetting = MainWin.Topmost;
                    MainWin.Topmost = false;
                });
                VersionSwitcherWrapper.ChangeChannel(appSettingsCtrl.SelectedReleaseChannel);
                Dispatcher.Invoke(() => MainWin.Close());
                return true;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                ex.ReportException();

                Dispatcher.Invoke(() =>
                {
                    MainWin.Topmost = previousTopmostSetting;
                    MessageDialog.Show(Properties.Resources.ConfigurationModeControl_VersionSwitcherException);
                });

                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        /// <summary>
        /// Enum for Setting modes
        /// </summary>
        internal enum SettingModes
        {
            Application,
            Connection,
            Test,
            About,
            Feedback,
            WhatsNew,
            Preview
        }

        /// <summary>
        /// Update UI based on Active Setting Mode.
        /// </summary>
        /// <param name="mode"></param>
        private void UpdateUIBasedOnSettingMode(SettingModes mode)
        {
            if (mode == SettingModes.Application || mode == SettingModes.Connection)
            {
                btnOk.Visibility = Visibility.Visible;    
            }
            else
            {
                btnOk.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Hide control and hilighter
        /// </summary>
        public void HideControl()
        {
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Show control and hilighter
        /// - if connection is true, routes to connection information
        /// </summary>
        public async void ShowControl(bool connection)
        {
            this.appSettingsCtrl.UpdateSaveButton = UpdateSaveButtonState;
            this.connectionCtrl.UpdateSaveButton = UpdateSaveButtonState;
            this.connectionCtrl.ShowSaveButton = ShowSaveButton;

            UpdateUIFromConfig();
            this.configSnapshot = (ConfigurationModel)Configuration.Clone();

            this.btnOk.IsEnabled = false;

            if (HelperMethods.GeneralFileBugVisibility == Visibility.Collapsed)
            {
                tbiConnection.Visibility = Visibility.Collapsed;
            }
            else if (connection)
            {
                tbiConnection.IsSelected = true;
            }

            await Dispatcher.InvokeAsync(() => (this.tcTabs.SelectedItem as UIElement).Focus(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Update Config UIs with config data if necessary and initiates view
        /// </summary>
        private void UpdateUIFromConfig()
        {
            appSettingsCtrl.UpdateFromConfig(Configuration);
            connectionCtrl.InitializeView();
        }

        /// <summary>
        /// Update UI when tab changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // selectionchanged event can bubble up from child UIElements, so we need to make sure only tcTabs is handled
            if (e.OriginalSource == this.tcTabs && e.RemovedItems.Count > 0 && e.AddedItems.Count > 0)
            {
                var mode = (SettingModes)(e.AddedItems[0] as TabItem).Tag;
                UpdateUIBasedOnSettingMode(mode);

                if (mode == SettingModes.Preview) // BeginInvoke stops dialog from blocking this event from returning
                    Dispatcher.BeginInvoke(new Action(() => MessageDialog.Show(Properties.Resources.TabControl_SelectionChangedDialogMessage)));
            }
        }
    }
}
