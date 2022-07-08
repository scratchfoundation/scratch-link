// <copyright file="ViewController.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using System;
using AppKit;
using Foundation;

/// <summary>
/// Example view controller.
/// </summary>
public partial class ViewController : NSViewController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViewController"/> class.
    /// </summary>
    /// <param name="handle">Opaque handle.</param>
    public ViewController(IntPtr handle)
        : base(handle)
    {
    }

    /// <summary>
    /// Gets or sets the object represented by this example UI.
    /// </summary>
    public override NSObject RepresentedObject
    {
        get
        {
            return base.RepresentedObject;
        }

        set
        {
            base.RepresentedObject = value;

            // Update the view, if already loaded.
        }
    }

    /// <summary>
    /// Called after the view controller's view has been loaded into memory.
    /// </summary>
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // Do any additional setup after loading the view.
    }
}
