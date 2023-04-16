// Copyright (c) 2021 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using GameDotNet.Core.Tools.Containers;

namespace GameDotNet.Core.Tools.Extensions;

/// <summary>
/// Extension methods associated with the IDisposable interface.
/// </summary>
public static class DisposableMixins
{
    /// <summary>
    /// Ensures the provided disposable is disposed with the specified <see cref="ICompositeDisposable"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the disposable.
    /// </typeparam>
    /// <param name="item">
    /// The disposable we are going to want to be disposed by the <see cref="ICompositeDisposable"/>.
    /// </param>
    /// <param name="compositeDisposable">
    /// The <see cref="ICompositeDisposable"/> to which <paramref name="item"/> will be added.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="compositeDisposable"/> is <c>null</c>.</exception>
    /// <returns>
    /// The disposable.
    /// </returns>
    public static T DisposeWith<T>(this T item, ICompositeDisposable compositeDisposable)
        where T : IDisposable
    {
        if (compositeDisposable is null)
            throw new ArgumentNullException(nameof(compositeDisposable));

        compositeDisposable.Add(item);
        return item;
    }

    /// <summary>
    /// If <paramref name="item"/> is disposable ensures it is disposed with the specified <see cref="ICompositeDisposable"/>
    /// </summary>
    /// <param name="item">
    /// The object that if disposable will be disposed by the <see cref="ICompositeDisposable"/>.
    /// </param>
    /// <param name="compositeDisposable">
    /// The <see cref="ICompositeDisposable"/> to which <paramref name="item"/> will be added.
    /// </param>
    /// <typeparam name="T">The type of the may be disposable</typeparam>
    /// <returns>
    /// The <paramref name="item"/>
    /// </returns>
    public static T DisposeIfWith<T>(this T item, ICompositeDisposable compositeDisposable)
    {
        if (compositeDisposable is IDisposable disposable)
            disposable.DisposeWith(compositeDisposable);

        return item;
    }

    /// <summary>
    /// Disposes an object if it inherits the <see cref="IDisposable"/> interface.
    /// </summary>
    /// <param name="obj">The object to dispose</param>
    /// <returns>
    /// True if object is disposable and was disposed, false otherwise
    /// </returns>
    public static bool DisposeIf(this object? obj)
    {
        if (obj is not IDisposable disposable)
            return false;

        disposable.Dispose();
        return true;
    }
}