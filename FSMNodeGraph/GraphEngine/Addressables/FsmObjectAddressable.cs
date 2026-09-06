using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.IO;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace FSMGraph
{
    /// <summary>【FSM 状态机播放器 - Addressables 版】—— 通过 Addressables 异步加载 FSM JSON</summary>
    public class FsmObjectAddressable : FsmObjectBase
    {
        /// <summary>Addressables 加载方式</summary>
        [SerializeField] public FSMLIBRARY.FsmAddressablesType addressablesType = FSMLIBRARY.FsmAddressablesType.PathName;

        /// <summary>Addressables 路径名称（addressablesType = PathName 时使用）</summary>
        [SerializeField] public string address = "";

        /// <summary>Addressables 资产引用（addressablesType = AssetReference 时使用）</summary>
        [SerializeField] public AssetReferenceT<TextAsset> assetRef = null;

        /// <summary>解析当前数据源（Addressables）的 JSON 文本，仅供 Inspector 调试按钮调用。用同步加载读取后用 Release 释放句柄，避免泄漏。</summary>
        public override string GetDataSourceJson() {
            try {
                if (addressablesType == FSMLIBRARY.FsmAddressablesType.PathName) {
                    if (string.IsNullOrEmpty(address)) return null;
                    var handle = Addressables.LoadAssetAsync<TextAsset>(address);
                    var asset = handle.WaitForCompletion();
                    return ReleaseAndRead(handle, asset);
                }
                else {
                    if (assetRef == null || !assetRef.RuntimeKeyIsValid()) return null;
                    var handle = Addressables.LoadAssetAsync<TextAsset>(assetRef);
                    var asset = handle.WaitForCompletion();
                    return ReleaseAndRead(handle, asset);
                }
            }
            catch (Exception e) {
                Debug.LogError($"[FsmObjectAddressable] 读取数据源 JSON 失败: {e.Message}");
                return null;
            }
        }

        /// <summary>读取并同步释放 Addressables 句柄，避免长期持有资源造成泄漏</summary>
        static string ReleaseAndRead(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<TextAsset> handle, TextAsset asset) {
            string result = asset != null ? asset.text : null;
            if (handle.IsValid())
                Addressables.Release(handle);
            return result;
        }

        /// <summary>通过 Addressables 异步加载 JSON → 反序列化 → 初始化引擎并播放</summary>
        public override async void Play() {
            // 首次调用 → 通过 Addressables 异步加载
            if (fsmData == null) {
                TextAsset jsonAsset = await LoadFromAddressablesAsync();
                if (jsonAsset == null) {
                    Debug.LogError("[FsmObjectAddressable] Addressables 加载失败！");
                    return;
                }
                DeserializeFromJson(jsonAsset.text);
            }

            if (fsmData == null) return;

            if (!Engine.IsInitialized)
                Engine.Initialize(fsmData, this);

            Engine.Start();
        }

        /// <summary>根据 addressablesType 选择对应的 Addressables 加载方式</summary>
        private async Task<TextAsset> LoadFromAddressablesAsync() {
            if (addressablesType == FSMLIBRARY.FsmAddressablesType.PathName) {
                if (string.IsNullOrEmpty(address)) {
                    Debug.LogError("[FsmObjectAddressable] address 为空！");
                    return null;
                }
                return await FetchAddressableAssetAsync(address);
            }
            else {
                if (assetRef == null || !assetRef.RuntimeKeyIsValid()) {
                    Debug.LogError("[FsmObjectAddressable] assetRef 无效！");
                    return null;
                }
                return await FetchAssetAsync(assetRef);
            }
        }

        #region 工具方法

        /// <summary>【加载资源】</summary>
        /// <param name="bundle">【AB包】</param>
        /// <param name="assetName">【资源名】</param>
        /// <returns>【获取的资源】</returns>
        public TextAsset FetchAsset(AssetBundle bundle, string assetName) {
            TextAsset res = (TextAsset)(bundle.LoadAsset(assetName, typeof(TextAsset)) as object);
            return res;
        }

        /// <summary>【加载Addressables资源（同步）】</summary>
        /// <param name="asset">【Addressable资源】</param>
        /// <returns>【获取的资源】</returns>
        public TextAsset FetchAssetSync(AssetReferenceT<TextAsset> asset) {
            return Addressables.LoadAssetAsync<TextAsset>(asset).WaitForCompletion();
        }
        /// <summary>【加载Addressables资源（异步）】</summary>
        /// <param name="asset">【Addressable资源】</param>
        /// <returns>【获取的资源】</returns>
        public async Task<TextAsset> FetchAssetAsync(AssetReferenceT<TextAsset> asset) {
            return await Addressables.LoadAssetAsync<TextAsset>(asset).Task;
        }

        /// <summary>【加载Addressables资源（同步）】</summary>
        /// <param name="address">【Addressable路径】</param>
        /// <returns>【获取的资源】</returns>
        public TextAsset FetchAddressableAssetSync(string address) {
            return Addressables.LoadAssetAsync<TextAsset>(address).WaitForCompletion();
        }
        /// <summary>【加载Addressables资源（异步）】</summary>
        /// <param name="address">【Addressable路径】</param>
        /// <returns>【获取的资源】</returns>
        public async Task<TextAsset> FetchAddressableAssetAsync(string address) {
            return await Addressables.LoadAssetAsync<TextAsset>(address).Task;
        }

        #endregion
    }
}
