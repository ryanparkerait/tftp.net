﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Tftp.Net.Transfer.States;
using System.IO;

namespace Tftp.Net.UnitTests.Transfer.States
{
    [TestFixture]
    class SendingState_Test
    {
        private TransferStub transfer;

        [SetUp]
        public void Setup()
        {
            transfer = new TransferStub(new MemoryStream(new byte[5000]));
            transfer.SetState(new Sending());
        }

        [Test]
        public void SendsPacket()
        {
            Assert.IsTrue(transfer.CommandWasSent(typeof(Data)));
        }

        [Test]
        public void ResendsPacket()
        {
            TransferStub transferWithLowTimeout = new TransferStub(new MemoryStream(new byte[5000]));
            transferWithLowTimeout.RetryTimeout = new TimeSpan(0);
            transferWithLowTimeout.SetState(new Sending());

            Assert.IsTrue(transferWithLowTimeout.CommandWasSent(typeof(Data)));
            transferWithLowTimeout.SentCommands.Clear();

            transferWithLowTimeout.OnTimer();
            Assert.IsTrue(transferWithLowTimeout.CommandWasSent(typeof(Data)));
        }

        [Test]
        public void TimeoutWhenNoAnswerIsReceivedAndRetryCountIsExceeded()
        {
            TransferStub transferWithLowTimeout = new TransferStub(new MemoryStream(new byte[5000]));
            transferWithLowTimeout.RetryTimeout = new TimeSpan(0);
            transferWithLowTimeout.RetryCount = 1;
            transferWithLowTimeout.SetState(new Sending());

            transferWithLowTimeout.OnTimer();
            Assert.IsFalse(transferWithLowTimeout.HadNetworkTimeout);
            transferWithLowTimeout.OnTimer();
            Assert.IsTrue(transferWithLowTimeout.HadNetworkTimeout);
        }

        [Test]
        public void HandlesAcknowledgment()
        {
            transfer.SentCommands.Clear();
            Assert.IsFalse(transfer.CommandWasSent(typeof(Data)));

            transfer.OnCommand(new Acknowledgement(1));
            Assert.IsTrue(transfer.CommandWasSent(typeof(Data)));
        }

        [Test]
        public void IgnoresWrongAcknowledgment()
        {
            transfer.SentCommands.Clear();
            Assert.IsFalse(transfer.CommandWasSent(typeof(Data)));

            transfer.OnCommand(new Acknowledgement(0));
            Assert.IsFalse(transfer.CommandWasSent(typeof(Data)));
        }

        [Test]
        public void RaisesProgress()
        {
            bool onProgressWasCalled = false;
            transfer.OnProgress += delegate(ITftpTransfer t, TftpTransferProgress progress) { Assert.AreEqual(transfer, t); Assert.IsTrue(progress.TransferredBytes > 0); onProgressWasCalled = true; };

            Assert.IsFalse(onProgressWasCalled);
            transfer.OnCommand(new Acknowledgement(1));
            Assert.IsTrue(onProgressWasCalled);
        }

        [Test]
        public void CanCancel()
        {
            transfer.Cancel(TftpErrorPacket.IllegalOperation);
            Assert.IsTrue(transfer.CommandWasSent(typeof(Error)));
            Assert.IsInstanceOf<Closed>(transfer.State);
        }

        [Test]
        public void HandlesError()
        {
            bool onErrorWasCalled = false;
            transfer.OnError += delegate(ITftpTransfer t, TftpTransferError error) { onErrorWasCalled = true; };

            Assert.IsFalse(onErrorWasCalled);
            transfer.OnCommand(new Error(123, "Test Error"));
            Assert.IsTrue(onErrorWasCalled);

            Assert.IsInstanceOf<Closed>(transfer.State);
        }
    }
}
