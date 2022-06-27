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

        /// <summary>
        /// Tab A 批量付款按钮
        /// </summary>
        private void BuckTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            var action = ((Button)sender).Content.ToString();
            try
            {
                BuckTransfer(action);
            }
            catch (Exception ex)
            {
                _logger.Error($"{action}：\t{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        private void Transfer_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            var action = ((Button)sender).Content.ToString();
            try
            {
                QueryAndTransfer(action, true);
            }
            catch (Exception ex)
            {
                _logger.Error($"{action}：\t{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        /// <summary>
        /// Tab B 查询余额按钮
        /// </summary>
        private void Query_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            var action = ((Button)sender).Content.ToString();
            try
            {
                QueryAndTransfer(action);
            }
            catch (Exception ex)
            {
                _logger.Error($"{action}：\t{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        /// <summary>
        /// Tab B 分发手续费按钮
        /// </summary>
        private void GAS_Click(object sender, RoutedEventArgs e)
        {
            var contractHash = UInt160.Parse(TabBContractHash.Text.Trim());
            var privateKeyList = TabBPrivateKey.Text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var toList = string.Empty;
            var client = new RpcClient(new Uri(TabCSeedNode.Text.Trim()));
            var api = new Nep17API(client);
            foreach (var wif in privateKeyList)
            {
                try
                {
                    var account = Utility.GetKeyPair(wif);
                    var from160 = Utility.GetScriptHash(account.PublicKey.ToString(), ProtocolSettings.Default);
                    var fromAddr = from160.ToAddress(0x35);
                    var balance = api.BalanceOfAsync(contractHash, from160).Result;
                    if(balance > 0)
                    toList += $"{fromAddr} 0.2\r\n";
                }
                catch (Exception)
                {
                    var warn = $"私钥错误{wif}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
                    return;
                }
            }

            TabAContractHash.Text = NativeContract.GAS.Hash.ToString();
            TabAToList.Text = toList;
            TabControl1.SelectedIndex = 0;
        }

        private void QueryAndTransfer(string? action, bool transfer = false)
        {
            TextBoxOutput.Text = string.Empty;
            var contractHash = UInt160.Parse(TabBContractHash.Text.Trim());
            var privateKeyList = TabBPrivateKey.Text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var toAddr = TabBToAddress.Text.Trim();
            var to160 = toAddr.ToScriptHash(0x35);

            var client = new RpcClient(new Uri(TabCSeedNode.Text.Trim()));
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
                    return;
                }
            }
            foreach (var wif in privateKeyList)
            {
                var account = Utility.GetKeyPair(wif);
                var from160 = Utility.GetScriptHash(account.PublicKey.ToString(), ProtocolSettings.Default);
                var fromAddr = from160.ToAddress(0x35);
                var balance = api.BalanceOfAsync(contractHash, from160).Result;
                var gas = Math.Round((double)api.BalanceOfAsync(NativeContract.GAS.Hash, from160).Result / 100000000.0, 4);
                //查询余额
                if (!transfer)
                {
                    var balanceDeci = (double)balance / Math.Pow(10, deci);
                    TextBoxOutput.Text += $"{fromAddr}\t{balanceDeci}{symbol}\t{gas}GAS\n";
                    _logger.Info($"{action}：\t{fromAddr}\t{toAddr}\t{balance}{symbol}\t{gas}GAS");
                }
                //归集
                if (transfer && balance > 0)
                {
                    if (fromAddr == toAddr) continue;
                    try
                    {
                        var tx = api.CreateTransferTxAsync(contractHash, account, to160, balance).Result;
                        client.SendRawTransactionAsync(tx);
                        TextBoxOutput.Text += $"{tx.Hash}\n";
                        _logger.Info($"{action}：\t{fromAddr}\t{toAddr}\t{balance}{symbol}\t{tx.Hash}");
                    }
                    catch (Exception e)
                    {
                        var warn = $"{action}：转账失败{e.Message}\n";
                        TextBoxOutput.Text += warn;
                        _logger.Warn(warn);
                    }
                }
            }
            TextBoxOutput.Text += "End\n";
        }

        private void BuckTransfer(string? action)
        {
            TextBoxOutput.Text = string.Empty;
            var fromPriKey = TabAPrivateKey.Text.Trim();
            //验证私钥格式
            try
            {
                Wallet.GetPrivateKeyFromWIF(fromPriKey);
            }
            catch (Exception)
            {
                var warn = $"私钥错误{fromPriKey}\n";
                TextBoxOutput.Text += warn;
                _logger.Warn(warn);
                return;
            }

            var fromAccount = Utility.GetKeyPair(fromPriKey);
            var fromAddress = Utility.GetScriptHash(fromAccount.PublicKey.ToString(), ProtocolSettings.Default);
            var contractHash = UInt160.Parse(TabAContractHash.Text.Trim());
            var client = new RpcClient(new Uri(TabCSeedNode.Text.Trim()));
            var api = new Nep17API(client);
            var deci = api.DecimalsAsync(contractHash).Result;
            var toList = TabAToList.Text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            //验证收款列表的格式
            var sum = .0;
            foreach (var to in toList)
            {
                try
                {
                    var toAddress = to.Split(' ')[0].ToScriptHash(0x35);
                    sum += Convert.ToDouble(to.Split(' ')[1]);
                }
                catch (Exception)
                {
                    var warn = $"收款列表格式错误：{to}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
                    return;
                }
            }
            var balance = (double)api.BalanceOfAsync(contractHash, fromAddress).Result / Math.Pow(10, deci);
            if (balance < sum)
            {
                var warn = $"付款地址余额不足：{balance} < {sum}\n";
                TextBoxOutput.Text += warn;
                _logger.Warn(warn);
                return;
            }

            foreach (var to in toList)
            {
                var toAddress = to.Split(' ')[0].ToScriptHash(0x35);
                var amount = new BigInteger(Convert.ToDouble(to.Split(' ')[1]) * Math.Pow(10, deci));
                try
                {
                    var tx = api.CreateTransferTxAsync(contractHash, fromAccount, toAddress, amount).Result;
                    client.SendRawTransactionAsync(tx);
                    TextBoxOutput.Text += $"{tx.Hash}\n";
                    _logger.Info($"{action}：\t{fromAddress}\t{toAddress}\t{amount}GAS\t{tx.Hash}");
                }
                catch (Exception ex)
                {
                    var warn = $"{action}：转账失败{ex.Message}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
                }
            }
            TextBoxOutput.Text += "End\n";
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AutoSize();
        }

        private void AutoSize()
        {
            TabAToList.Height = TabARow.ActualHeight;
            TabBPrivateKey.Height = TabBRow.ActualHeight;
        }
    }
}
