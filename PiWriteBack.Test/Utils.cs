using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Cognite.PiWriteBack.Test
{
    internal static class Utils
    {
        public static async Task WaitForCondition(Func<Task<bool>> condition, int seconds, Func<string> assertion)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (assertion == null) throw new ArgumentNullException(nameof(assertion));
            bool triggered = false;
            int i;
            for (i = 0; i < seconds * 5; i++)
            {
                if (await condition())
                {
                    triggered = true;
                    break;
                }

                await Task.Delay(200);
            }

            Assert.True(triggered, assertion());
        }

        public static string JsonElementToString(JsonElement elem)
        {
            return JsonSerializer.Serialize(elem, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        public static async Task WaitForCondition(Func<bool> condition, int seconds,
            string assertion = "Expected condition to trigger")
        {
            await WaitForCondition(() => Task.FromResult(condition()), seconds, () => assertion);
        }
        public static async Task WaitForCondition(Func<bool> condition, int seconds,
            Func<string> assertion)
        {
            await WaitForCondition(() => Task.FromResult(condition()), seconds, assertion);
        }
        public static async Task WaitForCondition(Func<Task<bool>> condition, int seconds,
            string assertion = "Expected condition to trigger")
        {
            await WaitForCondition(condition, seconds, () => assertion);
        }

        public static async Task RunWithTimeout(Task task, int seconds)
        {
            await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)));
            Assert.True(task.IsCompleted, "Task did not complete in time");
        }

        public static async Task RunWithTimeout(Func<Task> action, int seconds)
        {
            await RunWithTimeout(action(), seconds);
        }
    }
}
