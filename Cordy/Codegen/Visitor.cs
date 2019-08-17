﻿using Cordy.AST;
using Llvm.NET;
using Llvm.NET.DebugInfo;
using Llvm.NET.Instructions;
using Llvm.NET.Types;
using Llvm.NET.Values;
using System;
using System.Collections.Generic;

namespace Cordy.Codegen
{
    using DIBuilder = DebugInfoBuilder;
    using IRBuilder = InstructionBuilder;
    using Module = BitcodeModule;

    public class Visitor : CompilerPart
    {
        public BasicNode Visit(BasicNode node)
            => node.Accept(this);

        protected internal BasicNode VisitExtension(BasicNode node)
            => node.VisitChildren(this);

        #region Expression Nodes


        protected internal BasicNode VisitExpression(Expression node) =>
            //if (node.Args.Count > 2 || node.Args.Count < 1)
            //    throw new Exception("Wrong expression");
            //
            //LLVMValueRef n;
            //
            //switch (node.Args.Count)
            //{
            //    case 1:
            //
            //    case 2:
            //        Visit(node.Args[0]);
            //        var l = Stack.Pop();
            //
            //        Visit(node.Args[1]);
            //        var r = Stack.Pop();
            //
            //        if (node.Operator is PredefinedOperator)
            //        {
            //            //n = RunPredefinedOperator(l, r, (PredefinedOperator)node.Operator);
            //            Stack.Push(n);
            //            return node;
            //            //MethodInfo
            //        }
            //
            //}
            null;

        protected internal BasicNode VisitCall(CallFunctionNode node)
            => node;

        protected internal BasicNode VisitVariable(VarNode node)
        {
            for (var i = Depth; i >= 0; i++)
            {
                if (namedValues[i].TryGetValue(node.Name, out var value))
                {
                    Stack.Push(value);
                    return node;
                }
            }
            throw new Exception($"Unable to find variable {node.Name}");
        }


        protected internal BasicNode VisitInteger(IntegerNode node)
        {
            Stack.Push(Context.CreateConstant(Context.Int32Type, node.Value, node.Signed));
            return node;
        }

        protected internal BasicNode VisitFloat(FloatNode node)
        {
            Stack.Push(Context.CreateConstant(node.Value));
            return node;
        }
        protected internal BasicNode VisitType(TypeNode node)
            => node;


        [Obsolete("Not done")]
        protected internal BasicNode VisitVariableDef(VarDefinition node) => null;
        #endregion

        #region Code Blocks


        #region Simple Blocks

        protected internal BasicNode VisitCodeBlock(CodeBlock block)
        {
            foreach (var c in block.Childs)
                Visit(c);
            return block;
        }

        protected internal BasicNode VisitExprBlock(ExprBlock block)
        {
            foreach (var exp in block.Expressions)
                Visit(exp);
            return block;
        }
        #endregion

        protected internal BasicNode VisitReturnBlock(ReturnBlock block)
        {
            Visit(block.Childs[0]);
            IRBuilder.Return(Stack.Pop());
            return block;
        }

        #region Branching

        protected internal BasicNode VisitIfBlock(IfBlock block)
            => block;
        protected internal BasicNode VisitSwitchBlock(SwitchBlock block)
            => block;

        //TODO: Add goto
        //TODO: Add labels
        #endregion

        #region Loops

        protected internal BasicNode VisitForBlock(ForBlock block)
            => block;
        protected internal BasicNode VisitForeachBlock(ForeachBlock block)
            => block;
        protected internal BasicNode VisitWhileBlock(WhileBlock block)
            => block;
        protected internal BasicNode VisitDoWhileBlock(DoWhileBlock block)
            => block;

        #endregion

        #region Exception Handling

        protected internal BasicNode VisitTryBlock(TryBlock block)
            => block;

        #endregion

        #endregion

        #region Type Members

        protected internal BasicNode VisitPropertyDef(Property node)
            => node;
        protected internal BasicNode VisitOperatorDef(Operator node)
             => node;
        protected internal BasicNode VisitIndexerDef(Indexer node)
            => node;
        protected internal BasicNode VisitConstructorDef(Constructor node)
            => node;
        protected internal BasicNode VisitIndexerDef(Event node)
            => node;

        protected internal BasicNode VisitFunctionDef(Function node)
        {
            namedValues.Clear();
            Visit(node.Definition);

            var func = (IrFunction)Stack.Pop();
            //var entry = ((IrFunction)func).AppendBasicBlock("entry");
            IRBuilder.PositionAtEnd(func.AppendBasicBlock("entry"));

            try
            {
                Visit(node.Body);
            }
            catch (Exception)
            {
                Stack.Pop();
                func.EraseFromParent();
                throw;
            }

            func.Verify(out var err);
            if (err != null)
            {
                Error(err);
            }
            Stack.Push(func);
            return node;
        }

        protected internal BasicNode VisitPrototype(Definition node)
        {
            var count = node.Args.Count;
            var args = new ITypeRef[count];

            var func = Module.GetFunction(node.Name);
            if (func != null)
            {
                if (func.BasicBlocks.Count != 0) //TODO: allow redefines
                {
                    Error($"Member '{node.Name}' redefined");
                    return null;
                }
                if (func.Parameters.Count != count)
                {
                    Error($"Member '{node.Name}' redefined with another count of args");
                    return null;
                }
            }
            for (var i = 0; i < count; i++)
            {
                if (node.Args[i].Type.Template?.Count != 0)
                    Error($"Generic types aren't done yet. Result can differ from expectations");
                var t = Compiler.GetTypeByName(node.Args[i].Type.Name); //TODO: Make generics
                if (t != null)
                {
                    Error($"Unknown type '{node.Args[i].Type.Name}'");
                    return null;
                }
                args[i] = t;
            }
            //var rtype = Compiler.GetTypeByName(node.Type?.Name);
            var rtype = Context.Int32Type;
            //TODO: Apply storage modifiers
            //TODO: Get return type
            //TODO: Create different types of definitions
            func = Module.AddFunction(node.Name, Context.GetFunctionType(rtype, args, false));
            //func.Linkage(Linkage.);

            for (var i = 0; i < node.Args.Count; i++)
            {
                var name = node.Args[i].Name;
                func.Parameters[i].Name = name;
                namedValues[Depth][name] = func.Parameters[i];
            }

            Stack.Push(func);
            return node;
        }

        #endregion

        private Module Module;
        private IRBuilder IRBuilder;
        private DIBuilder DIBuilder;
        private Context Context;

        #region Logging
        public override string Stage { get; } = "CodeGen";

        public override string FileName { get; }

        public override (int, int) Pos { get; } = (0, 0);

        #endregion

        #region Values

        private readonly List<Dictionary<string, Value>> namedValues = new List<Dictionary<string, Value>>();

        public Stack<Value> Stack { get; } = new Stack<Value>();

        private int Depth;

        #endregion

        public Visitor(Module module, IRBuilder builder, Context context, string filename)
        {
            Context = context;
            Module = module;
            IRBuilder = builder;
            FileName = filename;
        }

        public void ClearStack() => Stack.Clear();

    }
}