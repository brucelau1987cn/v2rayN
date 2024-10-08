﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ServiceLib.Handler
{
    public class UpdateHandler
    {
        private Action<bool, string> _updateFunc;
        private Config _config;

        public event EventHandler<ResultEventArgs> AbsoluteCompleted;

        public class ResultEventArgs : EventArgs
        {
            public bool Success;
            public string Msg;
            public string Url;

            public ResultEventArgs(bool success, string msg, string url = "")
            {
                Success = success;
                Msg = msg;
                Url = url;
            }
        }

        public void CheckUpdateGuiN(Config config, Action<bool, string> update, bool preRelease)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Empty;

            DownloadHandler downloadHandle = new();
            downloadHandle.UpdateCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, ResUI.MsgDownloadV2rayCoreSuccessfully);
                    string fileName = Utils.GetTempPath(Utils.GetDownloadFileName(url));
                    fileName = Utils.UrlEncode(fileName);
                    _updateFunc(true, fileName);
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            downloadHandle.Error += (sender2, args) =>
            {
                _updateFunc(false, args.GetException().Message);
            };
            AbsoluteCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(ResUI.MsgParsingSuccessfully, ECoreType.v2rayN));
                    _updateFunc(false, args.Msg);

                    url = args.Url;
                    AskToDownload(downloadHandle, url, true).ContinueWith(task =>
                    {
                        _updateFunc(false, url);
                    });
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            _updateFunc(false, string.Format(ResUI.MsgStartUpdating, ECoreType.v2rayN));
            CheckUpdateAsync(ECoreType.v2rayN, preRelease);
        }

        public void CheckUpdateCore(ECoreType type, Config config, Action<bool, string> update, bool preRelease)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Empty;

            DownloadHandler downloadHandle = new();
            downloadHandle.UpdateCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, ResUI.MsgDownloadV2rayCoreSuccessfully);
                    _updateFunc(false, ResUI.MsgUnpacking);

                    try
                    {
                        _updateFunc(true, url);
                    }
                    catch (Exception ex)
                    {
                        _updateFunc(false, ex.Message);
                    }
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            downloadHandle.Error += (sender2, args) =>
            {
                _updateFunc(true, args.GetException().Message);
            };

            AbsoluteCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(ResUI.MsgParsingSuccessfully, type));
                    _updateFunc(false, args.Msg);

                    url = args.Url;
                    AskToDownload(downloadHandle, url, true).ContinueWith(task =>
                    {
                        _updateFunc(false, url);
                    });
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            _updateFunc(false, string.Format(ResUI.MsgStartUpdating, type));
            CheckUpdateAsync(type, preRelease);
        }

        public void UpdateSubscriptionProcess(Config config, string subId, bool blProxy, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;

            _updateFunc(false, ResUI.MsgUpdateSubscriptionStart);
            var subItem = LazyConfig.Instance.SubItems().OrderBy(t => t.sort).ToList();

            if (subItem == null || subItem.Count <= 0)
            {
                _updateFunc(false, ResUI.MsgNoValidSubscription);
                return;
            }

            Task.Run(async () =>
            {
                foreach (var item in subItem)
                {
                    string id = item.id.TrimEx();
                    string url = item.url.TrimEx();
                    string userAgent = item.userAgent.TrimEx();
                    string hashCode = $"{item.remarks}->";
                    if (Utils.IsNullOrEmpty(id) || Utils.IsNullOrEmpty(url) || (!Utils.IsNullOrEmpty(subId) && item.id != subId))
                    {
                        //_updateFunc(false, $"{hashCode}{ResUI.MsgNoValidSubscription}");
                        continue;
                    }
                    if (!url.StartsWith(Global.HttpsProtocol) && !url.StartsWith(Global.HttpProtocol))
                    {
                        continue;
                    }
                    if (item.enabled == false)
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgSkipSubscriptionUpdate}");
                        continue;
                    }

                    var downloadHandle = new DownloadHandler();
                    downloadHandle.Error += (sender2, args) =>
                    {
                        _updateFunc(false, $"{hashCode}{args.GetException().Message}");
                    };

                    _updateFunc(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

                    //one url
                    url = Utils.GetPunycode(url);
                    //convert
                    if (!Utils.IsNullOrEmpty(item.convertTarget))
                    {
                        var subConvertUrl = Utils.IsNullOrEmpty(config.constItem.subConvertUrl) ? Global.SubConvertUrls.FirstOrDefault() : config.constItem.subConvertUrl;
                        url = string.Format(subConvertUrl!, Utils.UrlEncode(url));
                        if (!url.Contains("target="))
                        {
                            url += string.Format("&target={0}", item.convertTarget);
                        }
                        if (!url.Contains("config="))
                        {
                            url += string.Format("&config={0}", Global.SubConvertConfig.FirstOrDefault());
                        }
                    }
                    var result = await downloadHandle.TryDownloadString(url, blProxy, userAgent);
                    if (blProxy && Utils.IsNullOrEmpty(result))
                    {
                        result = await downloadHandle.TryDownloadString(url, false, userAgent);
                    }

                    //more url
                    if (Utils.IsNullOrEmpty(item.convertTarget) && !Utils.IsNullOrEmpty(item.moreUrl.TrimEx()))
                    {
                        if (!Utils.IsNullOrEmpty(result) && Utils.IsBase64String(result!))
                        {
                            result = Utils.Base64Decode(result);
                        }

                        var lstUrl = item.moreUrl.TrimEx().Split(",") ?? [];
                        foreach (var it in lstUrl)
                        {
                            var url2 = Utils.GetPunycode(it);
                            if (Utils.IsNullOrEmpty(url2))
                            {
                                continue;
                            }

                            var result2 = await downloadHandle.TryDownloadString(url2, blProxy, userAgent);
                            if (blProxy && Utils.IsNullOrEmpty(result2))
                            {
                                result2 = await downloadHandle.TryDownloadString(url2, false, userAgent);
                            }
                            if (!Utils.IsNullOrEmpty(result2))
                            {
                                if (Utils.IsBase64String(result2!))
                                {
                                    result += Utils.Base64Decode(result2);
                                }
                                else
                                {
                                    result += result2;
                                }
                            }
                        }
                    }

                    if (Utils.IsNullOrEmpty(result))
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
                    }
                    else
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");
                        if (result?.Length < 99)
                        {
                            _updateFunc(false, $"{hashCode}{result}");
                        }

                        int ret = ConfigHandler.AddBatchServers(config, result, id, true);
                        if (ret <= 0)
                        {
                            Logging.SaveLog("FailedImportSubscription");
                            Logging.SaveLog(result);
                        }
                        _updateFunc(false,
                            ret > 0
                                ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                                : $"{hashCode}{ResUI.MsgFailedImportSubscription}");
                    }
                    _updateFunc(false, "-------------------------------------------------------");
                }

                _updateFunc(true, $"{ResUI.MsgUpdateSubscriptionEnd}");
            });
        }

        public void UpdateGeoFileAll(Config config, Action<bool, string> update)
        {
            Task.Run(async () =>
            {
                await UpdateGeoFile("geosite", _config, update);
                await UpdateGeoFile("geoip", _config, update);
            });
        }

        public void RunAvailabilityCheck(Action<bool, string> update)
        {
            Task.Run(async () =>
            {
                var time = await (new DownloadHandler()).RunAvailabilityCheck(null);

                update(false, string.Format(ResUI.TestMeOutput, time));
            });
        }

        #region private

        private async void CheckUpdateAsync(ECoreType type, bool preRelease)
        {
            try
            {
                var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(type);
                string url = coreInfo.coreReleaseApiUrl;

                var result = await (new DownloadHandler()).DownloadStringAsync(url, true, "");
                if (!Utils.IsNullOrEmpty(result))
                {
                    ResponseHandler(type, result, preRelease);
                }
                else
                {
                    Logging.SaveLog("StatusCode error: " + url);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                _updateFunc(false, ex.Message);
            }
        }

        /// <summary>
        /// 获取Core版本
        /// </summary>
        private SemanticVersion GetCoreVersion(ECoreType type)
        {
            try
            {
                var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(type);
                string filePath = string.Empty;
                foreach (string name in coreInfo.coreExes)
                {
                    string vName = Utils.GetExeName(name);
                    vName = Utils.GetBinPath(vName, coreInfo.coreType.ToString());
                    if (File.Exists(vName))
                    {
                        filePath = vName;
                        break;
                    }
                }

                if (!File.Exists(filePath))
                {
                    string msg = string.Format(ResUI.NotFoundCore, @"", "", "");
                    //ShowMsg(true, msg);
                    return new SemanticVersion("");
                }

                using Process p = new();
                p.StartInfo.FileName = filePath.AppendQuotes();
                p.StartInfo.Arguments = coreInfo.versionArg;
                p.StartInfo.WorkingDirectory = Utils.StartupPath();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                p.Start();
                p.WaitForExit(5000);
                string echo = p.StandardOutput.ReadToEnd();
                string version = string.Empty;
                switch (type)
                {
                    case ECoreType.v2fly:
                    case ECoreType.SagerNet:
                    case ECoreType.Xray:
                    case ECoreType.v2fly_v5:
                        version = Regex.Match(echo, $"{coreInfo.match} ([0-9.]+) \\(").Groups[1].Value;
                        break;

                    case ECoreType.clash:
                    case ECoreType.clash_meta:
                    case ECoreType.mihomo:
                        version = Regex.Match(echo, $"v[0-9.]+").Groups[0].Value;
                        break;

                    case ECoreType.sing_box:
                        version = Regex.Match(echo, $"([0-9.]+)").Groups[1].Value;
                        break;
                }
                return new SemanticVersion(version);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                _updateFunc(false, ex.Message);
                return new SemanticVersion("");
            }
        }

        private void ResponseHandler(ECoreType type, string gitHubReleaseApi, bool preRelease)
        {
            try
            {
                var gitHubReleases = JsonUtils.Deserialize<List<GitHubRelease>>(gitHubReleaseApi);
                var gitHubRelease = preRelease ? gitHubReleases?.First() : gitHubReleases?.First(r => r.Prerelease == false);
                var version = new SemanticVersion(gitHubRelease?.TagName!);
                var body = gitHubRelease?.Body;

                var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(type);
                SemanticVersion curVersion;
                string message;
                string? url;
                switch (type)
                {
                    case ECoreType.v2fly:
                    case ECoreType.SagerNet:
                    case ECoreType.Xray:
                    case ECoreType.v2fly_v5:
                        {
                            curVersion = GetCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, type, curVersion.ToVersionString("v"));
                            url = string.Format(GetUrlFromCore(coreInfo), version.ToVersionString("v"));
                            break;
                        }
                    case ECoreType.clash:
                    case ECoreType.clash_meta:
                    case ECoreType.mihomo:
                        {
                            curVersion = GetCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, type, curVersion);
                            url = string.Format(GetUrlFromCore(coreInfo), version.ToVersionString("v"));
                            break;
                        }
                    case ECoreType.sing_box:
                        {
                            curVersion = GetCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, type, curVersion.ToVersionString("v"));
                            url = string.Format(GetUrlFromCore(coreInfo), version.ToVersionString("v"), version);
                            break;
                        }
                    case ECoreType.v2rayN:
                        {
                            curVersion = new SemanticVersion(FileVersionInfo.GetVersionInfo(Utils.GetExePath()).FileVersion.ToString());
                            message = string.Format(ResUI.IsLatestN, type, curVersion);
                            url = string.Format(GetUrlFromCore(coreInfo), version);
                            break;
                        }
                    default:
                        throw new ArgumentException("Type");
                }

                if (curVersion >= version && version != new SemanticVersion(0, 0, 0))
                {
                    AbsoluteCompleted?.Invoke(this, new ResultEventArgs(false, message));
                    return;
                }

                AbsoluteCompleted?.Invoke(this, new ResultEventArgs(true, body, url));
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                _updateFunc(false, ex.Message);
            }
        }

        private string? GetUrlFromCore(CoreInfo? coreInfo)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => coreInfo?.coreDownloadUrlArm64,
                    Architecture.X86 => coreInfo?.coreDownloadUrl32,
                    Architecture.X64 => coreInfo?.coreDownloadUrl64,
                    _ => null,
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => coreInfo?.coreDownloadUrlLinuxArm64,
                    Architecture.X86 => coreInfo?.coreDownloadUrlLinux32,
                    Architecture.X64 => coreInfo?.coreDownloadUrlLinux64,
                    _ => null,
                };
            }
            return null;
        }

        private async Task AskToDownload(DownloadHandler downloadHandle, string url, bool blAsk)
        {
            //bool blDownload = false;
            //if (blAsk)
            //{
            //    if (UI.ShowYesNo(string.Format(ResUI.DownloadYesNo, url)) == MessageBoxResult.Yes)
            //    {
            //        blDownload = true;
            //    }
            //}
            //else
            //{
            //    blDownload = true;
            //}
            //if (blDownload)
            //{
            await downloadHandle.DownloadFileAsync(url, true, 600);
            //}
        }

        private async Task UpdateGeoFile(string geoName, Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Format(Global.GeoUrl, geoName);

            DownloadHandler downloadHandle = new();
            downloadHandle.UpdateCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, geoName));

                    try
                    {
                        string fileName = Utils.GetTempPath(Utils.GetDownloadFileName(url));
                        if (File.Exists(fileName))
                        {
                            string targetPath = Utils.GetBinPath($"{geoName}.dat");
                            File.Copy(fileName, targetPath, true);

                            File.Delete(fileName);
                            //_updateFunc(true, "");
                        }
                    }
                    catch (Exception ex)
                    {
                        _updateFunc(false, ex.Message);
                    }
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            downloadHandle.Error += (sender2, args) =>
            {
                _updateFunc(false, args.GetException().Message);
            };
            await AskToDownload(downloadHandle, url, false);
        }

        #endregion private
    }
}