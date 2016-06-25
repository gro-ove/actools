using System;
using System.IO;

namespace FirstFloor.ModernUI.Helpers {
    public class WrappingStream : Stream {
        /// <summary>
        /// Initializes a new instance of the <see cref="WrappingStream"/> class.
        /// </summary>
        /// <param name="streamBase">The wrapped stream.</param>
        public WrappingStream(Stream streamBase) {
            // check parameters
            if (streamBase == null)
                throw new ArgumentNullException(nameof(streamBase));

            WrappedStream = streamBase;
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <returns><c>true</c> if the stream supports reading; otherwise, <c>false</c>.</returns>
        public override bool CanRead => WrappedStream != null && WrappedStream.CanRead;

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <returns><c>true</c> if the stream supports seeking; otherwise, <c>false</c>.</returns>
        public override bool CanSeek => WrappedStream != null && WrappedStream.CanSeek;

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <returns><c>true</c> if the stream supports writing; otherwise, <c>false</c>.</returns>
        public override bool CanWrite => WrappedStream != null && WrappedStream.CanWrite;

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        public override long Length {
            get { ThrowIfDisposed(); return WrappedStream.Length; }
        }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        public override long Position {
            get { ThrowIfDisposed(); return WrappedStream.Position; }
            set { ThrowIfDisposed(); WrappedStream.Position = value; }
        }

        /// <summary>
        /// Begins an asynchronous read operation.
        /// </summary>
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
            ThrowIfDisposed();
            return WrappedStream.BeginRead(buffer, offset, count, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous write operation.
        /// </summary>
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
            ThrowIfDisposed();
            return WrappedStream.BeginWrite(buffer, offset, count, callback, state);
        }

        /// <summary>
        /// Waits for the pending asynchronous read to complete.
        /// </summary>
        public override int EndRead(IAsyncResult asyncResult) {
            ThrowIfDisposed();
            return WrappedStream.EndRead(asyncResult);
        }

        /// <summary>
        /// Ends an asynchronous write operation.
        /// </summary>
        public override void EndWrite(IAsyncResult asyncResult) {
            ThrowIfDisposed();
            WrappedStream.EndWrite(asyncResult);
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush() {
            ThrowIfDisposed();
            WrappedStream.Flush();
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position
        /// within the stream by the number of bytes read.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count) {
            ThrowIfDisposed();
            return WrappedStream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
        /// </summary>
        public override int ReadByte() {
            ThrowIfDisposed();
            return WrappedStream.ReadByte();
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin) {
            ThrowIfDisposed();
            return WrappedStream.Seek(offset, origin);
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        public override void SetLength(long value) {
            ThrowIfDisposed();
            WrappedStream.SetLength(value);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position
        /// within this stream by the number of bytes written.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count) {
            ThrowIfDisposed();
            WrappedStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
        /// </summary>
        public override void WriteByte(byte value) {
            ThrowIfDisposed();
            WrappedStream.WriteByte(value);
        }

        /// <summary>
        /// Gets the wrapped stream.
        /// </summary>
        /// <value>The wrapped stream.</value>
        protected Stream WrappedStream { get; private set; }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="WrappingStream"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing) {
            // doesn't close the base stream, but just prevents access to it through this WrappingStream
            if (disposing) {
                WrappedStream = null;
            }

            base.Dispose(disposing);
        }

        private void ThrowIfDisposed() {
            // throws an ObjectDisposedException if this object has been disposed
            if (WrappedStream == null) {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}