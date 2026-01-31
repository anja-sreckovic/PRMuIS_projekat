using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void DisconnectBtn_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            
        }

        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            SendCurrentMessage();
        }

        private void MessageBox_KeyDown(object sender, KeyEventArgs e)
        {
            
        }

        private void SendCurrentMessage()
        {
            
        }

        private void ReceiveLoop(CancellationToken token)
        {
            
        }

        private void Log(string line)
        {
            
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}
