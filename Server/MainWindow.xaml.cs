using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Server
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            
        }

        private void BroadcastBtn_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void ServerLoop(CancellationToken token)
        {
            
        }

        private void AcceptClient(Socket listener)
        {
            
        }

        private void ReceiveFromClient(Socket client)
        {
            
        }

        private void Broadcast(string message)
        {
            
        }

        private void SendToClient(Socket client, string msg)
        {
            
        }

        private void RemoveClient(Socket client, string reason)
        {
            
        }

        private static void SafeClose(Socket s)
        {
            
        }

        private void Log(string line)
        {
            
        }

        protected override void OnClosed(EventArgs e)
        {
            StopServer();
            base.OnClosed(e);
        }
    }
}
