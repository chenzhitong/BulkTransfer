using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Neo;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Utility = Neo.Network.RPC.Utility;

namespace BulkTransfer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Transfer_Click(object sender, RoutedEventArgs e)
        {
            QueryAndTransfer(true);
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            QueryAndTransfer();
        }

        private void QueryAndTransfer(bool transfer = false)
        {
            TextBoxOutput.Text = string.Empty;
            var seedNodeEndpoint = TextBoxSeedNode.Text;
            var contractHash = UInt160.Parse(TextBoxContractHash.Text);
            var privateKeyList = TextBoxPrivateKey.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var to = TextBoxToAddress.Text.Trim().ToScriptHash(0x35);

            var client = new RpcClient(new Uri(seedNodeEndpoint));
            var api = new Nep17API(client);
            var deci = api.DecimalsAsync(contractHash).Result;
            var symbol = api.SymbolAsync(contractHash).Result;

            foreach (var wif in privateKeyList)
            {
                try
                {
                    Wallet.GetPrivateKeyFromWIF(wif);
                }
                catch (Exception)
                {
                    TextBoxOutput.Text += $"私钥错误{wif}\n";
                    continue;
                }
                var account = Utility.GetKeyPair(wif);
                var address = Utility.GetScriptHash(account.PublicKey.ToString(), ProtocolSettings.Default);
                var balance = api.BalanceOfAsync(contractHash, address).Result;
                var gas = Math.Round((double)api.BalanceOfAsync(NativeContract.GAS.Hash, address).Result / 100000000.0, 2);
                if (!transfer)
                {
                    var balanceDeci = (double)balance / Math.Pow(10, deci);
                    TextBoxOutput.Text += $"{address.ToAddress(0x35)}\t{balanceDeci}{symbol}\t{gas}GAS\n";
                }
                if (transfer)
                {
                    try
                    {
                        var tx = api.CreateTransferTxAsync(contractHash, account, to, balance).Result;
                        client.SendRawTransactionAsync(tx);
                        TextBoxOutput.Text += $"{tx.Hash}\n";
                    }
                    catch (Exception e)
                    {
                        TextBoxOutput.Text += $"转账失败{e.Message}\n";
                    }
                }
            }
            TextBoxOutput.Text += "End\n";
        }

        private void GAS_Click(object sender, RoutedEventArgs e)
        {
            TextBoxOutput.Text = string.Empty;
            var gasPriKey = TextBoxGasPrivateKey.Text;
            try
            {
                Wallet.GetPrivateKeyFromWIF(gasPriKey);
            }
            catch (Exception)
            {
                TextBoxOutput.Text += $"私钥错误{gasPriKey}\n";
                return;
            }
            var fromAccount = Utility.GetKeyPair(gasPriKey);
            var seedNodeEndpoint = TextBoxSeedNode.Text;
            var contractHash = UInt160.Parse(TextBoxContractHash.Text);
            var client = new RpcClient(new Uri(seedNodeEndpoint));
            var api = new Nep17API(client);
            var privateKeyList = TextBoxPrivateKey.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (var wif in privateKeyList)
            {
                try
                {
                    Wallet.GetPrivateKeyFromWIF(wif);
                }
                catch (Exception)
                {
                    TextBoxOutput.Text += $"私钥错误{wif}\n";
                    continue;
                }
                var toAccount = Utility.GetKeyPair(wif);
                var toAddress = Utility.GetScriptHash(toAccount.PublicKey.ToString(), ProtocolSettings.Default);
                var balance = api.BalanceOfAsync(contractHash, toAddress).Result;
                var gas = (double)api.BalanceOfAsync(NativeContract.GAS.Hash, toAddress).Result / 100000000.0;
                if (balance > 0 && gas < 0.2)
                {
                    try
                    {
                        var tx = api.CreateTransferTxAsync(NativeContract.GAS.Hash, fromAccount, toAddress, 20000000).Result;
                        client.SendRawTransactionAsync(tx);
                        TextBoxOutput.Text += $"{tx.Hash}\n";
                    }
                    catch (Exception ex)
                    {
                        TextBoxOutput.Text += $"转账失败{ex.Message}\n";
                    }
                }
            }
            TextBoxOutput.Text += "End\n";
        }
    }
}
