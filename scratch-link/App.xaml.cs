// <copyright file="App.xaml.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

/// <summary>
/// The <see cref="App"/> class contains the cross-platform entry point for the application.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// This is the cross-platform entry point.
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        this.MainPage = new MainPage();
    }
}
