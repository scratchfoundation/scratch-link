// <copyright file="MainPage.xaml.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

/// <summary>
/// This class holds the main UI for the application.
/// </summary>
public partial class MainPage : ContentPage
{
    private int count = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        this.InitializeComponent();
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        this.count++;
        this.CounterLabel.Text = $"Current count: {this.count}";

        SemanticScreenReader.Announce(this.CounterLabel.Text);
    }
}
