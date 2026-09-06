namespace FSMGraph
{
    /// <summary>【节点数字比较方法】</summary>
    public enum NodeNumberCompareMethod
    {
        Equal, // 等于
        NotEqual, // 不等于
        Greater, // 大于
        Less, // 小于
        GreaterEqual, // 大于等于
        LessEqual, // 小于等于
    }

    /// <summary>【节点单数字计算方法】</summary>
    public enum NodeSingleMathMethod
    {
        Add_1, // 加1
        Sub_1, // 减1
        Power_2, // 平方
        Sqrt_2, // 开方
        Absolute, // 绝对值
        Negate, // 取负
        Ceil, // 向上取整
        Floor, // 向下取整
        Round, // 四舍五入
        Frac, // 取小数部分
        Sign, // 取符号
    }

    /// <summary>【节点多数字计算方法】</summary>
    public enum NodeMultiMathMethod
    {
        Add, // 加法
        Subtract, // 减法
        Multiply, // 乘法
        Divide, // 除法
        Modulus, // 取余
        Power, // 指数
        Sqrt, // 开方
        Log, // 对数
    }

    /// <summary>【节点向量计算方法】</summary>
    public enum NodeVectorMathMethod
    {
        Add, // 加法
        Subtract, // 减法
        Dot_Product, // 点积
        Cross_Product, // 叉积
        A_Normalize, // 归一化
        A_Magnitude, // 模长
        Distance, // 距离
    }

    /// <summary>【节点三角函数计算方法】</summary>
    public enum NodeTrigonometryMethod
    {
        Sin, // 正弦
        Cos, // 余弦
        Tan, // 正切
        Cot, // 余切
    }

    /// <summary>【节点数学常量】</summary>
    public enum NodeMathConstant
    {
        PI, // 圆周率
        E, // 自然数
        Sqrt_2, // 根号2
    }

    /// <summary>【节点颜色通道】</summary>
    public enum NodeColorChannel
    {
        Red, // 红色
        Green, // 绿色
        Blue, // 蓝色
        Alpha, // 透明度
    }

    /// <summary>【节点正则表达式方法】</summary>
    public enum NodeRegularExpressionMethod
    {
        Remove, // 移除
        Replace, // 替换
    }
}
