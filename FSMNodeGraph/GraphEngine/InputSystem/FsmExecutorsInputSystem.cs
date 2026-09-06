using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;

namespace FSMGraph
{
    #region 条件节点

    /// <summary>NListenInput: 输入监听 —— 通过 InputSystem 轮询 Action 状态。无 InputSystem 时所有端口返回 false。</summary>
    public class ListenInputExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NListenInput";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            string actionMap = ctx.Engine.PullValue(ctx, node.Rid, "actionMap").AsString();
            string actionName = ctx.Engine.PullValue(ctx, node.Rid, "actionName").AsString();

            if (string.IsNullOrEmpty(actionMap) || string.IsNullOrEmpty(actionName))
                return Value(false);

            var action = InputSystem.actions?.FindActionMap(actionMap)?.FindAction(actionName);
            if (action == null) return Value(false);

            bool value = portName switch {
                "onStarted"   => action.WasPressedThisFrame(),
                "onPerformed" => action.IsPressed(),
                "onCanceled"  => action.WasReleasedThisFrame(),
                _             => false
            };

            return Value(value);
        }

        private static FsmExecutionResult Value(bool b) =>
            FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = b });
    }

    #endregion
}

