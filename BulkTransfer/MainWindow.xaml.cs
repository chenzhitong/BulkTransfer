using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        private RpcClient LoadClient()
        {
            return new RpcClient(new Uri(TabCSeedNode.Text.Trim()), null, null, ProtocolSettings.Load("config.json"));
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
                if (BuckTransfer(action))
                    MessageBox.Show("发送成功");
            }
            catch (Exception ex)
            {
                _logger.Error($"{action}：{ex.Message}");
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
                _logger.Error($"{action}：{ex.Message}");
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
            var client = LoadClient();
            var api = new Nep17API(client);

            foreach (var wif in privateKeyList)
            {
                try
                {
                    var account = Utility.GetKeyPair(wif);
                    var from160 = Utility.GetScriptHash(account.PublicKey.ToString(), ProtocolSettings.Default);
                    var fromAddr = from160.ToAddress(ProtocolSettings.Default.AddressVersion);
                    var balance = api.BalanceOfAsync(contractHash, from160).Result;
                    if (balance > 0)
                        toList += $"{fromAddr} 0.2\r\n";
                }
                catch (Exception)
                {
                    TextBoxOutput.Text += $"私钥错误：{wif}\n";
                    return;
                }
            }

            TabAContractHash.Text = NativeContract.GAS.Hash.ToString();
            TabAToList.Text = toList;
            TabControl1.SelectedIndex = 0;
            MessageBox.Show("已自动填写了GAS的资产哈希、收款地址和转账金额，请根据需要进行编辑，然后点击批量付款", "请在该页面继续操作");
        }

        /// <summary>
        /// Tab B 归集按钮
        /// </summary>
        private void Pooled_Click(object sender, RoutedEventArgs e)
        {
            if (_pause) return;
            _pause = true;
            var action = ((Button)sender).Content.ToString();
            try
            {
                if (QueryAndTransfer(action, true))
                    MessageBox.Show("发送成功");
            }
            catch (Exception ex)
            {
                _logger.Error($"{action}：{ex.Message}");
                MessageBox.Show(ex.Message);
            }
            _pause = false;
        }

        private bool QueryAndTransfer(string? action, bool transfer = false)
        {
            TextBoxOutput.Text = string.Empty;
            var contractHash = UInt160.Parse(TabBContractHash.Text.Trim());
            var privateKeyList = TabBPrivateKey.Text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var toAddr = TabBToAddress.Text.Trim();
            var to160 = toAddr.ToScriptHash(ProtocolSettings.Default.AddressVersion);

            var client = LoadClient();
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
                    TextBoxOutput.Text += $"私钥错误：{wif}\n";
                    return false;
                }
            }
            foreach (var wif in privateKeyList)
            {
                var account = Utility.GetKeyPair(wif);
                var from160 = Utility.GetScriptHash(account.PublicKey.ToString(), ProtocolSettings.Default);
                var fromAddr = from160.ToAddress(ProtocolSettings.Default.AddressVersion);
                var balance = api.BalanceOfAsync(contractHash, from160).Result;
                var gas = Math.Round((double)api.BalanceOfAsync(NativeContract.GAS.Hash, from160).Result / Math.Pow(10, NativeContract.GAS.Decimals), 4);
                //查询余额
                if (!transfer)
                {
                    var balanceDeci = (double)balance / Math.Pow(10, deci);
                    if (contractHash == NativeContract.GAS.Hash)
                    {
                        TextBoxOutput.Text += $"{fromAddr}\t{balanceDeci}{symbol}\n";
                        _logger.Info($"{action}：{fromAddr}\t{toAddr}\t{balanceDeci}{symbol}");
                    }
                    else
                    {
                        TextBoxOutput.Text += $"{fromAddr}\t{balanceDeci}{symbol}\t{gas}GAS\n";
                        _logger.Info($"{action}：{fromAddr}\t{toAddr}\t{balanceDeci}{symbol}\t{gas}GAS");
                    }

                }
                //归集
                if (transfer && balance > 0 && fromAddr != toAddr)
                {
                    try
                    {
                        if (contractHash == NativeContract.GAS.Hash)
                        {
                            if (balance > 20000000)
                                balance -= 20000000;
                            else
                                continue;
                        }
                        var tx = api.CreateTransferTxAsync(contractHash, account, to160, balance).Result;
                        client.SendRawTransactionAsync(tx);
                        TextBoxOutput.Text += $"{tx.Hash}\n";
                        var balanceDeci = (double)balance / Math.Pow(10, deci);

                        _logger.Info($"{action}：{fromAddr}\t{toAddr}\t{balanceDeci}{symbol}\t{tx.Hash}");
                    }
                    catch (Exception e)
                    {
                        var warn = $"{action}：转账失败\t{e.Message}\n";
                        TextBoxOutput.Text += warn;
                        _logger.Warn(warn);
                    }
                }
            }
            return true;
        }

        private bool BuckTransfer(string? action)
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
                TextBoxOutput.Text += $"私钥错误{fromPriKey}\n";
                return false;
            }

            var fromAccount = Utility.GetKeyPair(fromPriKey);
            var from160 = Utility.GetScriptHash(fromAccount.PublicKey.ToString(), ProtocolSettings.Default);
            var fromAddr = from160.ToAddress(ProtocolSettings.Default.AddressVersion);
            var contractHash = UInt160.Parse(TabAContractHash.Text.Trim());
            var client = LoadClient();
            var api = new Nep17API(client);
            var deci = api.DecimalsAsync(contractHash).Result;
            var toList = TabAToList.Text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var symbol = api.SymbolAsync(contractHash).Result;
            //验证收款列表的格式
            var sum = .0;
            foreach (var to in toList)
            {
                try
                {
                    var toAddress = to.Split(' ')[0].ToScriptHash(ProtocolSettings.Default.AddressVersion);
                    sum += Convert.ToDouble(to.Split(' ')[1]);
                }
                catch (Exception)
                {
                    TextBoxOutput.Text += $"收款列表格式错误：{to}\n";
                    return false;
                }
            }
            var balance = (double)api.BalanceOfAsync(contractHash, from160).Result / Math.Pow(10, deci);
            if (balance < sum)
            {
                var warn = $"付款地址余额不足：{balance} < {sum}\n";
                TextBoxOutput.Text += warn;
                _logger.Warn(warn);
                return false;
            }

            foreach (var to in toList)
            {
                var toAddr = to.Split(' ')[0];
                var to160 = toAddr.ToScriptHash(ProtocolSettings.Default.AddressVersion);
                var amountDeci = Convert.ToDouble(to.Split(' ')[1]);
                var amount = new BigInteger(amountDeci * Math.Pow(10, deci));
                try
                {
                    var tx = api.CreateTransferTxAsync(contractHash, fromAccount, to160, amount).Result;
                    client.SendRawTransactionAsync(tx);
                    TextBoxOutput.Text += $"{tx.Hash}\n";

                    var balanceDeci = (double)balance / Math.Pow(10, deci);
                    _logger.Info($"{action}：{fromAddr}\t{toAddr}\t{amountDeci}{symbol}\t{tx.Hash}");
                }
                catch (Exception ex)
                {
                    var warn = $"{action}：转账失败\t{ex.Message}\n";
                    TextBoxOutput.Text += warn;
                    _logger.Warn(warn);
                }
            }
            return true;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AutoSize();
        }

        private void TabControl1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var t = new Thread(AutoSize);
            t.Start();
        }

        private void AutoSize()
        {
            Dispatcher.BeginInvoke(() =>
            {
                TabAToList.Height = TabARow.ActualHeight;
                TabBPrivateKey.Height = TabBRow.ActualHeight;
            });

        }

        private void Ping_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var height = LoadClient().GetBlockCountAsync().Result;
                MessageBox.Show($"连接成功，高度为 {height}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败\t{ex.Message}");
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("notepad.exe", "config.json");
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            Process.Start("explorer.exe", link.NavigateUri.ToString());
        }
    }
}
