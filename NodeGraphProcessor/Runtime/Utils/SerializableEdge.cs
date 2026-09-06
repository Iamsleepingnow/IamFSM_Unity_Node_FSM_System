using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GraphProcessor
{
	[System.Serializable]
	public class SerializableEdge : ISerializationCallbackReceiver
	{
		public string	GUID;

		[SerializeField]
		BaseGraph		owner;

		[SerializeField]
		public string			inputNodeGUID;
		[SerializeField]
		public string			outputNodeGUID;

		[System.NonSerialized]
		public BaseNode	inputNode;

		[System.NonSerialized]
		public NodePort	inputPort;
		[System.NonSerialized]
		public NodePort outputPort;

		//temporary object used to send port to port data when a custom input/output function is used.
		[System.NonSerialized]
		public object	passThroughBuffer;

		[System.NonSerialized]
		public BaseNode	outputNode;

		public string	inputFieldName;
		public string	outputFieldName;

		// Use to store the id of the field that generate multiple ports
		public string	inputPortIdentifier;
		public string	outputPortIdentifier;

		public SerializableEdge() {}

		public static SerializableEdge CreateNewEdge(BaseGraph graph, NodePort inputPort, NodePort outputPort)
		{
			SerializableEdge	edge = new SerializableEdge();

			edge.owner = graph;
			edge.GUID = System.Guid.NewGuid().ToString();
			edge.inputNode = inputPort.owner;
			edge.inputFieldName = inputPort.fieldName;
			edge.outputNode = outputPort.owner;
			edge.outputFieldName = outputPort.fieldName;
			edge.inputPort = inputPort;
			edge.outputPort = outputPort;
			edge.inputPortIdentifier = inputPort.portData.identifier;
			edge.outputPortIdentifier = outputPort.portData.identifier;

			return edge;
		}

		public void OnBeforeSerialize()
		{
			// 仅当节点引用可用时才更新 GUID；节点引用暂时不可用时保留旧 GUID
			// —— 旧值可能是上次有效序列化时写入的，反序列化后可凭此重连
			// 永远不要清空 GUID：一旦清空，边将永久断连
			if (outputNode != null)
				outputNodeGUID = outputNode.GUID;
			if (inputNode != null)
				inputNodeGUID = inputNode.GUID;
		}

		public void OnAfterDeserialize() {}

		//here our owner have been deserialized
		public void Deserialize()
		{
			if (!owner.nodesPerGUID.ContainsKey(outputNodeGUID) || !owner.nodesPerGUID.ContainsKey(inputNodeGUID))
				return ;

			outputNode = owner.nodesPerGUID[outputNodeGUID];
			inputNode = owner.nodesPerGUID[inputNodeGUID];
			inputPort = inputNode.GetPort(inputFieldName, inputPortIdentifier);
			outputPort = outputNode.GetPort(outputFieldName, outputPortIdentifier);
		}

		public override string ToString() => $"{outputNode.name}:{outputPort.fieldName} -> {inputNode.name}:{inputPort.fieldName}";
	}
}
