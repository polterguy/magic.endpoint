/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Linq;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.endpoint.services.slots.misc
{
    /// <summary>
    /// [mime.add] slot for associating a file extension with a MIME type.
    /// </summary>
    [Slot(Name = "mime.add")]
    public class AddMimeType : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            signaler.Signal("eval", input);
            HttpExecutorAsync.AddMimeType(input.GetEx<string>(), input.Children.First().GetEx<string>());
        }
    }
}
