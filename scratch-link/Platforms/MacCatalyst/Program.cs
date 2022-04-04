// <copyright file="Program.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using ObjCRuntime;
using UIKit;

/// <summary>
/// This class is the main entry point for MacCatalyst.
/// </summary>
public class Program
{
    // This method is the main entry point of the application for MacCatalyst.
    private static void Main(string[] args)
    {
        // if you want to use a different Application Delegate class from "AppDelegate"
        // you can specify it here.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
