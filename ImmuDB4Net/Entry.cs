/*
Copyright 2022 CodeNotary, Inc. All rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
namespace ImmuDB;

public class Entry
{
    public ulong Tx { get; private set; }

    public byte[] Key { get; private set; }

    public byte[] Value { get; private set; }

    public KVMetadata? Metadata { get; private set; }

    public Reference? ReferencedBy { get; private set; }

    private Entry(byte[] key, byte[] value) {
        Key = key;
        Value = value;
    }

    public static Entry ValueOf(ImmudbProxy.Entry e)
    {
        ImmudbProxy.Entry proxyInst = e ?? ImmudbProxy.Entry.DefaultInstance;
        Entry entry = new Entry(proxyInst.Key.ToByteArray(), proxyInst.Value.ToByteArray());
        entry.Tx = proxyInst.Tx;

        if (proxyInst.Metadata != null)
        {
            entry.Metadata = KVMetadata.ValueOf(proxyInst.Metadata);
        }

        if (proxyInst.ReferencedBy != null)
        {
            entry.ReferencedBy = Reference.ValueOf(proxyInst.ReferencedBy);
        }

        return entry;
    }

    public byte[] getEncodedKey()
    {
        if (ReferencedBy == null)
        {
            return Utils.WrapWithPrefix(Key, Consts.SET_KEY_PREFIX);
        }

        return Utils.WrapWithPrefix(ReferencedBy.Key, Consts.SET_KEY_PREFIX);
    }

    public byte[] DigestFor(int version)
    {
        KV kv;

        if (ReferencedBy == null)
        {
            kv = new KV(
                    getEncodedKey(),
                    Metadata,
                    Utils.WrapWithPrefix(Value, Consts.PLAIN_VALUE_PREFIX)
            );
        }
        else
        {
            kv = new KV(
                    getEncodedKey(),
                    ReferencedBy.Metadata,
                    Utils.WrapReferenceValueAt(Utils.WrapWithPrefix(Key, Consts.SET_KEY_PREFIX), ReferencedBy.AtTx)
            );
        }

        return kv.DigestFor(version);
    }

}
