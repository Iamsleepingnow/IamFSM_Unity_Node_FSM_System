using UnityEngine;
using System;
using System.Linq.Expressions;

namespace GraphProcessor
{
	public static class TypeExtension
	{
		public static bool IsReallyAssignableFrom(this Type type, Type otherType)
		{
			if (type.IsAssignableFrom(otherType))
				return true;
			if (otherType.IsAssignableFrom(type))
				return true;

			try
			{
				var v = Expression.Variable(otherType);
				var expr = Expression.Convert(v, type);
				// 表达式树构建成功，说明该转换在 C# 中是合法的
				// （涵盖内置隐式/显式数值转换、用户自定义转换等）
				return true;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}

	}
}