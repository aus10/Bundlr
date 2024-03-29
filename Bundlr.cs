﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bundlr 
{
    public static class ScriptBundler
    {
        public static Bundle GetBundler(string htmlFilePath)
        {
            return new Bundle
            {
                HtmlFilePath = htmlFilePath
            };
        }

        public static Bundle AddFile(this Bundle bundle, string path)
        {
            bundle.FilePathList.Add(path);
            return bundle;
        }

        public static Bundle AddJsDirectory(this Bundle bundle, string path)
        {
            foreach (string file in Directory.EnumerateFiles(path, "*.js"))
            {
                bundle.FilePathList.Add(file);
            }
            return bundle;
        }

        public static Bundle AddCssDirectory(this Bundle bundle, string path)
        {
            foreach (string file in Directory.EnumerateFiles(path, "*.css"))
            {
                bundle.FilePathList.Add(file);
            }
            return bundle;
        }

        public static void BundleJs(this Bundle bundle, string bundeledScriptUrl, bool minify = false)
        {
            BundleFiles(bundeledScriptUrl, bundle.FilePathList, minify);
            ReplaceTags(bundle.HtmlFilePath, bundle.FilePathList.Select(Path.GetFileName).ToList(), "text/javascript", bundeledScriptUrl, "<script type='text/javascript' src='{0}'></script>");
        }

        public static void BundleCss(this Bundle bundle, string bundeledScriptUrl)
        {
            BundleFiles(bundeledScriptUrl, bundle.FilePathList, false);
            ReplaceTags(bundle.HtmlFilePath, bundle.FilePathList.Select(Path.GetFileName).ToList(), "stylesheet", bundeledScriptUrl, "<link rel='stylesheet' href='{0}'>");
        }

        private static void BundleFiles(string bundledFilePath, List<string> scriptsToBundle, bool minify)
        {
            var sb = new StringBuilder();

            foreach (var script in scriptsToBundle)
            {
                if (Path.GetFileName(script) == Path.GetFileName(bundledFilePath))
                    continue;

                sb.Append(File.ReadAllText(script));
            }

            var concatinatedScripts = minify
                ? new JsMinifier().Minify(sb.ToString())
                : sb.ToString();

            File.WriteAllText(bundledFilePath, concatinatedScripts);
        }

        private static void ReplaceTags(string htmlFilePath, List<string> scriptsToReplace, string tagIdentifier, string bundledScriptPath, string htmlTagTemplate)
        {
            var text = new StringBuilder();
            bool placedBundledScriptTag = false;
            foreach (string s in File.ReadAllLines(htmlFilePath))
            {
                bool removeScriptTag = false;
                foreach (var script in scriptsToReplace)
                {
                    if (s.Contains(script) && s.Contains(tagIdentifier))
                    {
                        removeScriptTag = true;

                        if (!placedBundledScriptTag)
                        {
                            var fullScriptPath = Path.Combine(Environment.CurrentDirectory, bundledScriptPath);
                            var fullHtmlPath = Path.Combine(Environment.CurrentDirectory, Path.GetDirectoryName(htmlFilePath));

                            var relativeScriptPath = GetRelativePath(fullScriptPath, fullHtmlPath);

                            text.AppendLine(string.Format(htmlTagTemplate, relativeScriptPath));
                            placedBundledScriptTag = true;
                        }
                    }
                }

                if (!removeScriptTag)
                {
                    text.AppendLine(s);
                }
            }

            using (var file = new StreamWriter(File.Create(htmlFilePath)))
            {
                file.Write(text.ToString());
            }
        }

        private static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString());//.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    public class Bundle
    {
        public string HtmlFilePath;
        public List<string> FilePathList = new List<string>();
    }

    /* This is a .NET port of the Douglas Crockford's JSMin 'C' project.
     * The author's copyright message is reproduced below.
     */

    /* jsmin.c
       2013-03-29
    Copyright (c) 2002 Douglas Crockford  (www.crockford.com)
    Permission is hereby granted, free of charge, to any person obtaining a copy of
    this software and associated documentation files (the "Software"), to deal in
    the Software without restriction, including without limitation the rights to
    use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
    of the Software, and to permit persons to whom the Software is furnished to do
    so, subject to the following conditions:
    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.
    The Software shall be used for Good, not Evil.
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
    */
    public sealed class JsMinifier
    {
        const int EOF = -1;

        private StringReader _reader;
        private StringWriter _writer;

        private int _theA;
        private int _theB;
        private int _theLookahead = EOF;
        private int _theX = EOF;
        private int _theY = EOF;

        /// <summary>
        /// Synchronizer of minification
        /// </summary>
        private readonly object _minificationSynchronizer = new object();


        /// <summary>
        /// Removes a comments and unnecessary whitespace from JavaScript code
        /// </summary>
        /// <param name="content">JavaScript content</param>
        /// <returns>Minified JavaScript content</returns>
        public String Minify(string content)
        {
            string minifiedContent;

            lock (_minificationSynchronizer)
            {
                _theA = 0;
                _theB = 0;
                _theLookahead = EOF;
                _theX = EOF;
                _theY = EOF;

                using (_reader = new StringReader(content))
                using (_writer = new StringWriter())
                {
                    InnerMinify();
                    _writer.Flush();

                    minifiedContent = _writer.ToString().TrimStart();
                }

                _reader = null;
                _writer = null;
            }

            return minifiedContent;
        }

        /// <summary>
        /// Returns a true if the character is a letter, digit, underscore, dollar sign, or non-ASCII character
        /// </summary>
        /// <param name="c">The character</param>
        /// <returns>Result of check</returns>
        private static bool IsAlphanum(int c)
        {
            return ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'Z') || c == '_' || c == '$' || c == '\\' ||
                c > 126);
        }

        /// <summary>
        /// Returns a next character from input stream. Watch out for lookahead.
        /// If the character is a control character, translate it to a space or linefeed.
        /// </summary>
        /// <returns>The character</returns>
        private int Get()
        {
            int c = _theLookahead;
            _theLookahead = EOF;

            if (c == EOF)
            {
                c = _reader.Read();
            }

            if (c >= ' ' || c == '\n' || c == EOF)
            {
                return c;
            }

            if (c == '\r')
            {
                return '\n';
            }

            return ' ';
        }

        /// <summary>
        /// Gets a next character without getting it
        /// </summary>
        /// <returns>The character</returns>
        private int Peek()
        {
            _theLookahead = Get();

            return _theLookahead;
        }

        /// <summary>
        /// Gets a next character, excluding comments. 
        /// <code>Peek()</code> is used to see if a '/' is followed by a '/' or '*'.
        /// </summary>
        /// <returns>The character</returns>
        private int Next()
        {
            int c = Get();

            if (c == '/')
            {
                switch (Peek())
                {
                    case '/':
                        for (; ; )
                        {
                            c = Get();

                            if (c <= '\n')
                            {
                                break;
                            }
                        }

                        break;
                    case '*':
                        Get();

                        while (c != ' ')
                        {
                            switch (Get())
                            {
                                case '*':
                                    if (Peek() == '/')
                                    {
                                        Get();
                                        c = ' ';
                                    }

                                    break;
                                case EOF:
                                    throw new Exception("Unterminated comment.");
                            }
                        }

                        break;
                }
            }

            _theY = _theX;
            _theX = c;

            return c;
        }

        /// <summary>
        /// Do something! What you do is determined by the argument:
        ///		1 - Output A. Copy B to A. Get the next B.
        ///		2 - Copy B to A. Get the next B. (Delete A).
        ///		3 - Get the next B. (Delete B).
        /// <code>Action</code> treats a string as a single character.
        /// Wow! <code>Action</code> recognizes a regular expression 
        /// if it is preceded by <code>(</code> or , or <code>=</code>.
        /// </summary>
        /// <param name="d">Action type</param>
        private void Action(int d)
        {
            if (d == 1)
            {
                Put(_theA);

                if (
                    (_theY == '\n' || _theY == ' ') &&
                    (_theA == '+' || _theA == '-' || _theA == '*' || _theA == '/') &&
                    (_theB == '+' || _theB == '-' || _theB == '*' || _theB == '/')
                )
                {
                    Put(_theY);
                }
            }

            if (d <= 2)
            {
                _theA = _theB;

                if (_theA == '\'' || _theA == '"' || _theA == '`')
                {
                    for (; ; )
                    {
                        Put(_theA);
                        _theA = Get();

                        if (_theA == _theB)
                        {
                            break;
                        }

                        if (_theA == '\\')
                        {
                            Put(_theA);
                            _theA = Get();
                        }

                        if (_theA == EOF)
                        {
                            throw new Exception("Unterminated string literal.");
                        }
                    }
                }
            }

            if (d <= 3)
            {
                _theB = Next();
                if (_theB == '/' && (
                    _theA == '(' || _theA == ',' || _theA == '=' || _theA == ':' ||
                    _theA == '[' || _theA == '!' || _theA == '&' || _theA == '|' ||
                    _theA == '?' || _theA == '+' || _theA == '-' || _theA == '~' ||
                    _theA == '*' || _theA == '/' || _theA == '{' || _theA == '\n'
                ))
                {
                    Put(_theA);

                    if (_theA == '/' || _theA == '*')
                    {
                        Put(' ');
                    }

                    Put(_theB);

                    for (; ; )
                    {
                        _theA = Get();

                        if (_theA == '[')
                        {
                            for (; ; )
                            {
                                Put(_theA);
                                _theA = Get();

                                if (_theA == ']')
                                {
                                    break;
                                }

                                if (_theA == '\\')
                                {
                                    Put(_theA);
                                    _theA = Get();
                                }

                                if (_theA == EOF)
                                {
                                    throw new Exception("Unterminated set in Regular Expression literal.");
                                }
                            }
                        }
                        else if (_theA == '/')
                        {
                            switch (Peek())
                            {
                                case '/':
                                case '*':
                                    throw new Exception("Unterminated set in Regular Expression literal.");
                            }

                            break;
                        }
                        else if (_theA == '\\')
                        {
                            Put(_theA);
                            _theA = Get();
                        }

                        if (_theA == EOF)
                        {
                            throw new Exception("Unterminated Regular Expression literal.");
                        }

                        Put(_theA);
                    }

                    _theB = Next();
                }
            }
        }

        /// <summary>
        /// Copies a input to the output, deleting the characters which are insignificant to JavaScript.
        /// Comments will be removed. Tabs will be replaced with spaces.
        /// Carriage returns will be replaced with linefeeds. Most spaces and linefeeds will be removed.
        /// </summary>
        private void InnerMinify()
        {
            if (Peek() == 0xEF)
            {
                Get();
                Get();
                Get();
            }

            _theA = '\n';
            Action(3);

            while (_theA != EOF)
            {
                switch (_theA)
                {
                    case ' ':
                        Action(IsAlphanum(_theB) ? 1 : 2);
                        break;
                    case '\n':
                        switch (_theB)
                        {
                            case '{':
                            case '[':
                            case '(':
                            case '+':
                            case '-':
                            case '!':
                            case '~':
                                Action(1);
                                break;
                            case ' ':
                                Action(3);
                                break;
                            default:
                                Action(IsAlphanum(_theB) ? 1 : 2);
                                break;
                        }

                        break;
                    default:
                        switch (_theB)
                        {
                            case ' ':
                                Action(IsAlphanum(_theA) ? 1 : 3);
                                break;
                            case '\n':
                                switch (_theA)
                                {
                                    case '}':
                                    case ']':
                                    case ')':
                                    case '+':
                                    case '-':
                                    case '"':
                                    case '\'':
                                    case '`':
                                        Action(1);
                                        break;
                                    default:
                                        Action(IsAlphanum(_theA) ? 1 : 3);
                                        break;
                                }

                                break;
                            default:
                                Action(1);
                                break;
                        }

                        break;
                }
            }
        }

        #region Methods for substitution methods of the C language
        /// <summary>
        /// Puts a character to output stream 
        /// </summary>
        /// <param name="c">The character</param>
        private void Put(int c)
        {
            _writer.Write((char)c);
        }
        #endregion
    }
}


