﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using WinAlfred.Commands;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace WinAlfred
{
    public partial class MainWindow
    {
        private KeyboardHook hook = new KeyboardHook();
        private NotifyIcon notifyIcon;
        private Command cmdDispatcher;
        Storyboard progressBarStoryboard = new Storyboard();
        private bool queryHasReturn = false;
        SelectedRecords selectedRecords = new SelectedRecords();

        public MainWindow()
        {
            InitializeComponent();

            hook.KeyPressed += OnHotKey;
            hook.RegisterHotKey(XModifierKeys.Alt, Keys.Space);
            resultCtrl.resultItemChangedEvent += resultCtrl_resultItemChangedEvent;
            ThreadPool.SetMaxThreads(30, 10);
            InitProgressbarAnimation();

            ChangeStyles(Settings.Instance.Theme);
        }

        private void WakeupApp()
        {
            //After hide winalfred in the background for a long time. It will become very slow in the next show.
            //This is caused by the Virtual Mermory Page Mechanisam. So, our solution is execute some codes in every min
            //which may prevent sysetem uninstall memory from RAM to disk.

            System.Timers.Timer t = new System.Timers.Timer(1000 * 60 * 3) { AutoReset = true, Enabled = true };
            t.Elapsed += (o, e) => Dispatcher.Invoke(new Action(() =>
                {
                    if (Visibility != Visibility.Visible)
                    {
                        double oldLeft = Left;
                        Left = 20000;
                        ShowWinAlfred();
                        cmdDispatcher.DispatchCommand(new Query("qq"), false);
                        HideWinAlfred();
                        Left = oldLeft;
                    }
                }));
        }

        private void InitProgressbarAnimation()
        {
            DoubleAnimation da = new DoubleAnimation(progressBar.X2, Width + 100, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            DoubleAnimation da1 = new DoubleAnimation(progressBar.X1, Width, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            progressBarStoryboard.Children.Add(da);
            progressBarStoryboard.Children.Add(da1);
            progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            progressBar.Visibility = Visibility.Hidden;
            progressBar.BeginStoryboard(progressBarStoryboard);
        }

        private void InitialTray()
        {
            notifyIcon = new NotifyIcon { Text = "WinAlfred", Icon = Properties.Resources.app, Visible = true };
            notifyIcon.Click += (o, e) => ShowWinAlfred();
            System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("Open");
            open.Click += (o, e) => ShowWinAlfred();
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("Exit");
            exit.Click += (o, e) => CloseApp();
            System.Windows.Forms.MenuItem[] childen = { open, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);
        }

        private void resultCtrl_resultItemChangedEvent()
        {
            //Height = resultCtrl.pnlContainer.ActualHeight + tbQuery.Height + tbQuery.Margin.Top + tbQuery.Margin.Bottom;
            resultCtrl.Margin = resultCtrl.GetCurrentResultCount() > 0 ? new Thickness { Top = 10 } : new Thickness { Top = 0 };
        }

        private void OnHotKey(object sender, KeyPressedEventArgs e)
        {
            if (!IsVisible)
            {
                ShowWinAlfred();
            }
            else
            {
                HideWinAlfred();
            }
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            resultCtrl.Dirty = true;
            Dispatcher.DelayInvoke("UpdateSearch",
               o =>
               {
                   Dispatcher.DelayInvoke("ClearResults", i =>
                   {
                       // first try to use clear method inside resultCtrl, which is more closer to the add new results
                       // and this will not bring splash issues.After waiting 30ms, if there still no results added, we
                       // must clear the result. otherwise, it will be confused why the query changed, but the results
                       // didn't.
                       if (resultCtrl.Dirty) resultCtrl.Clear();
                   }, TimeSpan.FromMilliseconds(30), null);
                   var q = new Query(tbQuery.Text);
                   cmdDispatcher.DispatchCommand(q);
                   queryHasReturn = false;
                   if (Plugins.HitThirdpartyKeyword(q))
                   {
                       Dispatcher.DelayInvoke("ShowProgressbar", originQuery =>
                       {
                           if (!queryHasReturn && originQuery == tbQuery.Text)
                           {
                               StartProgress();
                           }
                       }, TimeSpan.FromSeconds(1), tbQuery.Text);
                   }

               }, TimeSpan.FromMilliseconds(300));
        }

        private void StartProgress()
        {
            progressBar.Visibility = Visibility.Visible;
        }

        private void StopProgress()
        {
            progressBar.Visibility = Visibility.Hidden;
        }

        private void HideWinAlfred()
        {
            Hide();
        }

        private void ShowWinAlfred()
        {
            Show();
            Activate();
            tbQuery.Focus();
            tbQuery.SelectAll();
        }

        public void ParseArgs(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                switch (args[0])
                {
                    case "reloadWorkflows":
                        Plugins.Init(this);
                        break;

                    case "query":
                        if (args.Length > 1)
                        {
                            string query = args[1];
                            tbQuery.Text = query;
                            tbQuery.SelectAll();
                        }
                        break;
                }
            }
        }

        private void SetAutoStart(bool IsAtuoRun)
        {
            //string LnkPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "//WinAlfred.lnk";
            //if (IsAtuoRun)
            //{
            //    WshShell shell = new WshShell();
            //    IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(LnkPath);
            //    shortcut.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            //    shortcut.WorkingDirectory = Environment.CurrentDirectory;
            //    shortcut.WindowStyle = 1; //normal window
            //    shortcut.Description = "WinAlfred";
            //    shortcut.Save();
            //}
            //else
            //{
            //    System.IO.File.Delete(LnkPath);
            //}
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Plugins.Init(this);
            cmdDispatcher = new Command(this);
            InitialTray();
            selectedRecords.LoadSelectedRecords();
            SetAutoStart(true);
            WakeupApp();
            //var engine = new Jurassic.ScriptEngine();
            //MessageBox.Show(engine.Evaluate("5 * 10 + 2").ToString());
        }

        private void TbQuery_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    HideWinAlfred();
                    e.Handled = true;
                    break;

                case Key.Down:
                    resultCtrl.SelectNext();
                    e.Handled = true;
                    break;

                case Key.Up:
                    resultCtrl.SelectPrev();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    Result result = resultCtrl.AcceptSelect();
                    if (result != null)
                    {
                        selectedRecords.AddSelect(result);
                        if (!result.DontHideWinAlfredAfterSelect)
                        {
                            HideWinAlfred();
                        }
                    }
                    e.Handled = true;
                    break;
            }
        }

        public void OnUpdateResultView(List<Result> list)
        {
            queryHasReturn = true;
            progressBar.Dispatcher.Invoke(new Action(StopProgress));
            if (list.Count > 0)
            {
                //todo:this used be opened to users, it's they choise use it or not in thier workflows
                list.ForEach(o =>
                {
                    if (o.AutoAjustScore) o.Score += selectedRecords.GetSelectedCount(o);
                });
                resultCtrl.Dispatcher.Invoke(new Action(() =>
                {
                    List<Result> l = list.Where(o => o.OriginQuery != null && o.OriginQuery.RawQuery == tbQuery.Text).ToList();
                    resultCtrl.AddResults(l);
                }));
            }
        }

        public void ChangeStyles(string themeName)
        {
            ResourceDictionary dict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Themes/" + themeName + ".xaml")
            };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        #region Public API

        //Those method can be invoked by plugins

        public void ChangeQuery(string query)
        {
            tbQuery.Text = query;
            tbQuery.CaretIndex = tbQuery.Text.Length;
        }

        public void CloseApp()
        {
            notifyIcon.Visible = false;
            Environment.Exit(0);
        }

        public void HideApp()
        {
            HideWinAlfred();
        }

        public void ShowApp()
        {
            ShowWinAlfred();
        }

        public void ShowMsg(string title, string subTitle, string iconPath)
        {
            Msg m = new Msg { Owner = GetWindow(this) };
            m.Show(title, subTitle, iconPath);
        }

        public void OpenSettingDialog()
        {
            SettingWidow s = new SettingWidow(this);
            s.Show();
        }

        #endregion


    }
}