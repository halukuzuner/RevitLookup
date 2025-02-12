﻿// Copyright 2003-2023 by Autodesk, Inc.
// 
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
// 
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
// 
// Use, duplication, or disclosure by the U.S. Government is subject to
// restrictions set forth in FAR 52.227-19 (Commercial Computer
// Software - Restricted Rights) and DFAR 252.227-7013(c)(1)(ii)
// (Rights in Technical Data and Computer Software), as applicable.

using System.Windows;
using RevitLookup.Services.Contracts;
using RevitLookup.ViewModels.Pages;
using RevitLookup.Views.Dialogs;
using Wpf.Ui.Appearance;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Navigation;

namespace RevitLookup.Views.Pages;

public sealed partial class AboutView : INavigableView<AboutViewModel>
{
    private readonly IContentDialogService _dialogService;

    public AboutView(AboutViewModel viewModel, IContentDialogService dialogService, ISettingsService settingsService)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = this;
        _dialogService = dialogService;
        Theme.Apply(this, settingsService.Theme, settingsService.Background);
    }

    public AboutViewModel ViewModel { get; }

    private async void ShowSoftwareDialog(object sender, RoutedEventArgs e)
    {
        var dialog = _dialogService.CreateDialog();
        dialog.Title = "Third-Party Software";
        dialog.DialogWidth = 500;
        dialog.DialogHeight = 465;
        dialog.Content = new OpenSourceDialog();
        await dialog.ShowAsync();
    }
}