﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.Text;

namespace Microsoft.CodeAnalysis.Editor.Shared.Extensions
{
    internal static class ITextBufferEditExtensions
    {
        /// <summary>
        /// Logs exceptions thrown during <see cref="ITextBufferEdit.Apply"/> as we look for issues.
        /// </summary>
        /// <param name="edit"></param>
        /// <returns></returns>
        public static ITextSnapshot ApplyAndLogExceptions(this ITextBufferEdit edit)
        {
            try
            {
                return edit.Apply();
            }
            catch (Exception e) when (ErrorReporting.FatalError.ReportWithoutCrash(e))
            {
                // Since we don't know what is causing this yet, I don't feel safe that catching
                // will not cause some further downstream failure. So we'll continue to propagate.
                throw;
            }
        }
    }
}
