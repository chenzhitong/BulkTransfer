using Neo;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.Wallets;
using NLog;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using Utility = Neo.Network.RPC.Utility;

namespace BulkTransfer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Logger _logger;
        bool _pause = false;

        public MainWindow()
        {
            InitializeComponent();
            _logger = LogManager.GetCurrentClassLogger();
        }

        private void Transfer_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            try
            {
                QueryAndTransfer(true);
            }
            catch (Exception ex)
            {
                _logger.Error($"归集：\t{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            try
            {
                QueryAndTransfer();
            }
            catch (Exception ex)
            {
                _logger.Error($"查询余额：\t{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        private void GAS_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            try
            {
                DistributeGAS();
            }
            catch (Exception ex)
            {
                _logger.Error($"分发手续费：\t{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        private void NEP17_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            try
            {
                DistributeNEP17(1080160090);
            }
            catch (Exception ex)
            {
                _logger.Error($"分发NEP-17：\t{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        private void QueryAndTransfer(bool transfer = false)
        {
            TextBoxOutput.Text = string.Empty;
            var seedNodeEndpoint = TextBoxSeedNode.Text;
            var contractHash = UInt160.Parse(TextBoxContractHash.Text);
            var privateKeyList = TextBoxPrivateKey.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var toAddr = TextBoxToAddress.Text.Trim();
            var to160 = toAddr.ToScriptHash(0x35);

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
                    var warn = $"私钥错误{wif}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
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
                    _logger.Info($"查询\t{address}\t{toAddr}\t{balance}{symbol}\t{gas}GAS");
                }
                if (transfer && balance > 0)
                {
                    try
                    {
                        var tx = api.CreateTransferTxAsync(contractHash, account, to160, balance).Result;
                        client.SendRawTransactionAsync(tx);
                        TextBoxOutput.Text += $"{tx.Hash}\n";
                        _logger.Info($"归集：\t{address}\t{toAddr}\t{balance}{symbol}\t{tx.Hash}");
                    }
                    catch (Exception e)
                    {
                        var warn = $"归集：转账失败{e.Message}\n";
                        TextBoxOutput.Text += warn;
                        _logger.Warn(warn);
                    }
                }
            }
            TextBoxOutput.Text += "End\n";
        }

        private void DistributeGAS()
        {
            TextBoxOutput.Text = string.Empty;
            var gasPriKey = TextBoxGasPrivateKey.Text;
            try
            {
                Wallet.GetPrivateKeyFromWIF(gasPriKey);
            }
            catch (Exception)
            {
                var warn = $"私钥错误{gasPriKey}\n";
                TextBoxOutput.Text += warn;
                _logger.Warn(warn);
                return;
            }
            var fromAccount = Utility.GetKeyPair(gasPriKey);
            var fromAddress = Utility.GetScriptHash(fromAccount.PublicKey.ToString(), ProtocolSettings.Default);
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
                    var warn = $"私钥错误{wif}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
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
                        var amount = new BigInteger((0.2 - gas) * 100000000);
                        var tx = api.CreateTransferTxAsync(NativeContract.GAS.Hash, fromAccount, toAddress, amount).Result;
                        client.SendRawTransactionAsync(tx);
                        TextBoxOutput.Text += $"{tx.Hash}\n";
                        _logger.Info($"分发手续费：\t{fromAddress}\t{toAddress}\t{amount}GAS\t{tx.Hash}");
                    }
                    catch (Exception ex)
                    {
                        var warn = $"分发手续费：转账失败{ex.Message}\n";
                        TextBoxOutput.Text += warn;
                        _logger.Warn(warn);
                    }
                }
            }
            TextBoxOutput.Text += "End\n";
        }

        private void DistributeNEP17(BigInteger amount)
        {
            TextBoxOutput.Text = string.Empty;
            var gasPriKey = TextBoxGasPrivateKey.Text;
            try
            {
                Wallet.GetPrivateKeyFromWIF(gasPriKey);
            }
            catch (Exception)
            {
                var warn = $"私钥错误{gasPriKey}\n";
                TextBoxOutput.Text += warn;
                _logger.Warn(warn);
                return;
            }
            var fromAccount = Utility.GetKeyPair(gasPriKey);
            var fromAddress = Utility.GetScriptHash(fromAccount.PublicKey.ToString(), ProtocolSettings.Default);
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
                    var warn = $"私钥错误{wif}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
                    continue;
                }
                var toAccount = Utility.GetKeyPair(wif);
                var toAddress = Utility.GetScriptHash(toAccount.PublicKey.ToString(), ProtocolSettings.Default);
                try
                {
                    var tx = api.CreateTransferTxAsync(contractHash, fromAccount, toAddress, amount).Result;
                    client.SendRawTransactionAsync(tx);
                    TextBoxOutput.Text += $"{tx.Hash}\n";
                    _logger.Info($"分发NEP-17：\t{fromAddress}\t{toAddress}\t{amount}GAS\t{tx.Hash}");
                }
                catch (Exception ex)
                {
                    var warn = $"分发NEP-17：转账失败{ex.Message}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
                }
            }
            TextBoxOutput.Text += "End\n";
        }
    }
}
