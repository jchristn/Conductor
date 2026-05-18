namespace Test.Shared.Touchstone
{
    using System;
    using System.Reflection;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ReflectionTestInvoker
    {
        internal static async Task ExecuteAsync(Type testType, MethodInfo testMethod, CancellationToken cancellationToken)
        {
            if (testType == null) throw new ArgumentNullException(nameof(testType));
            if (testMethod == null) throw new ArgumentNullException(nameof(testMethod));

            object instance = Activator.CreateInstance(testType)
                ?? throw new InvalidOperationException("Unable to instantiate " + testType.FullName);

            Exception executionException = null;
            Exception cleanupException = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InvokeIfPresentAsync(instance, "InitializeAsync").ConfigureAwait(false);
                await InvokeIfPresentAsync(instance, "Initialize").ConfigureAwait(false);
                await InvokeMethodAsync(instance, testMethod).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                executionException = UnwrapException(ex);
            }
            finally
            {
                try
                {
                    await InvokeIfPresentAsync(instance, "DisposeAsync").ConfigureAwait(false);
                    if (instance is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception ex)
                {
                    cleanupException = UnwrapException(ex);
                }
            }

            if (executionException != null && cleanupException != null)
                throw new AggregateException(executionException, cleanupException);

            if (executionException != null)
                ExceptionDispatchInfo.Capture(executionException).Throw();

            if (cleanupException != null)
                ExceptionDispatchInfo.Capture(cleanupException).Throw();
        }

        private static async Task InvokeIfPresentAsync(object instance, string methodName)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method == null)
                return;

            await InvokeMethodAsync(instance, method).ConfigureAwait(false);
        }

        private static async Task InvokeMethodAsync(object instance, MethodInfo method)
        {
            object result = method.Invoke(instance, null);

            if (method.ReturnType == typeof(void))
                return;

            if (method.ReturnType == typeof(Task))
            {
                await ((Task)result).ConfigureAwait(false);
                return;
            }

            if (method.ReturnType == typeof(ValueTask))
            {
                await ((ValueTask)result).ConfigureAwait(false);
                return;
            }

            throw new InvalidOperationException(
                "Unsupported return type '" + method.ReturnType.FullName + "' for " + method.DeclaringType?.FullName + "." + method.Name);
        }

        private static Exception UnwrapException(Exception exception)
        {
            if (exception is TargetInvocationException targetInvocationException
                && targetInvocationException.InnerException != null)
            {
                return targetInvocationException.InnerException;
            }

            return exception;
        }
    }
}
