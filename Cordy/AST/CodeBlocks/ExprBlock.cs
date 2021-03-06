﻿using System.Collections.Generic;

namespace Cordy.AST
{
    /// <summary>
    /// Block of code represented by a sequence of expressions
    /// </summary>
    public class ExprBlock : CodeBlock
    {
        public ExprBlock(List<ExprNode> exprs, int indent) : base(null, indent) => Expressions = exprs;

        public List<ExprNode> Expressions { get; }
    }
}
