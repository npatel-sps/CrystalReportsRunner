﻿using CommandLine;

using LijsDev.CrystalReportsRunner.Abstractions;

using PipeMethodCalls;

using System;
using System.Threading;
using System.Windows.Forms;

namespace LijsDev.CrystalReportsRunner.Core
{
    public class Shell
    {
        public class Options
        {
            [Option('n', "pipe-name", Required = true, HelpText = "The Named Pipe this instance should connect to.")]
            public string PipeName { get; set; }
        }

        private readonly ManualResetEvent _waitHandle = new(false);
        private readonly IReportViewer _reportViewer;
        private Form? _mainForm;

        public Shell(IReportViewer reportViewer)
        {
            _reportViewer = reportViewer;
        }

        public void StartListening(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);

            if (result.Tag == ParserResultType.Parsed)
            {
                var thread = new Thread(() => OpenConnection(result.Value));
                thread.Start();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                _mainForm = CreateInvisibleForm();
                _waitHandle.Set();
                Application.Run(_mainForm);
            }
            else
            {
                // TODO: Add logger with NLog
                foreach (var error in result.Errors)
                {
                    Console.WriteLine(error.ToString());
                }
            }
        }

        private Form CreateInvisibleForm()
        {
            return new Form
            {
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = 0,
                WindowState = FormWindowState.Minimized,
            };
        }

        public void RunCodeOnUIThread(Action action)
        {
            if (_mainForm is not null)
            {
                _mainForm.BeginInvoke(action);
            }
        }

        private async void OpenConnection(Options options)
        {
            using var pipeClient = new PipeClientWithCallback<ICrystalReportsCaller, ICrystalReportsRunner>(
                     new JsonNetPipeSerializer(),
                     ".",
                     options.PipeName,
                     () => new WinFormsReportRunner(_reportViewer, RunCodeOnUIThread));

            _waitHandle.WaitOne();
            await pipeClient.ConnectAsync();
            await pipeClient.WaitForRemotePipeCloseAsync();
        }
    }
}