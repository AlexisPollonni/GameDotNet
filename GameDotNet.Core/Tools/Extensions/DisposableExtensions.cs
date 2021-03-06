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
    /// Ensures the provided disposable is disposed with the specified <see cref="DisposableList"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the disposable.
    /// </typeparam>
    /// <param name="item">
    /// The disposable we are going to want to be disposed by the DisposableList.
    /// </param>
    /// <param name="compositeDisposable">
    /// The <see cref="DisposableList"/> to which <paramref name="item"/> will be added.
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
}