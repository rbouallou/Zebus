﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Abc.Zebus.Util.Extensions;
using StructureMap;

namespace Abc.Zebus.Dispatch
{
    public class SyncMessageHandlerInvoker : MessageHandlerInvoker
    {
        private readonly IContainer _container;
        private readonly Action<object, IMessage> _handleAction;

        public SyncMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup = true)
            : this(container, handlerType, messageType, shouldBeSubscribedOnStartup, GenerateHandleAction(handlerType, messageType))
        {
        }

        protected SyncMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup, Action<object, IMessage> handleAction)
            : base(handlerType, messageType, shouldBeSubscribedOnStartup)
        {
            _container = container;
            _handleAction = handleAction;
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            var handler = CreateHandler(invocation.Context);
            using (invocation.SetupForInvocation(handler))
            {
                _handleAction(handler, invocation.Message);
            }
        }

        internal object CreateHandler(MessageContext messageContext)
        {
            return CreateHandler(_container, messageContext);
        }

        public static Action<object, IMessage> GenerateHandleAction(Type handlerType, Type messageType)
        {
            var handleMethod = GetHandleMethodOrThrow(handlerType, messageType);
            ThrowsIfAsyncVoid(handlerType, handleMethod);

            var o = Expression.Parameter(typeof(object), "o");
            var m = Expression.Parameter(typeof(IMessage), "m");
            var body = Expression.Call(Expression.Convert(o, handlerType), handleMethod, Expression.Convert(m, messageType));
            var lambda = Expression.Lambda(typeof(Action<object, IMessage>), body, o, m);

            return (Action<object, IMessage>)lambda.Compile();
        }

        private static MethodInfo GetHandleMethodOrThrow(Type handlerType, Type messageType)
        {
            var handleMethod = handlerType.GetMethod("Handle", new[] { messageType });
            if (handleMethod == null)
                throw new InvalidProgramException(string.Format("The given type {0} is not an IMessageHandler<{1}>", handlerType.Name, messageType.Name));

            return handleMethod;
        }

        private static void ThrowsIfAsyncVoid(Type handlerType, MethodInfo handleMethod)
        {
            if (handleMethod.ReturnType == typeof(void) && handleMethod.GetAttribute<AsyncStateMachineAttribute>(true) != null)
            {
                var error = string.Format("The message handler {0} has an async void Handle method. If you think there are valid use cases for this, please discuss it with the dev team", handlerType);
                throw new InvalidProgramException(error);
            }
        }
    }
}