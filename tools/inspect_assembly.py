"""Raft Assembly-CSharp metadata inspector (read-only, no assembly loading)."""
import sys
import dnfile

PATH = r"C:\Program Files (x86)\Steam\steamapps\common\Raft\Raft_Data\Managed\Assembly-CSharp.dll"

ELEMENT = {
    0x01: "void", 0x02: "bool", 0x03: "char", 0x04: "sbyte", 0x05: "byte",
    0x06: "short", 0x07: "ushort", 0x08: "int", 0x09: "uint", 0x0A: "long",
    0x0B: "ulong", 0x0C: "float", 0x0D: "double", 0x0E: "string",
    0x18: "IntPtr", 0x19: "UIntPtr", 0x1C: "object",
}


class Asm:
    def __init__(self, path=PATH):
        self.dn = dnfile.dnPE(path)
        self.md = self.dn.net.mdtables
        self.typedefs = self.md.TypeDef.rows
        self.typerefs = self.md.TypeRef.rows
        self.fields = self.md.Field.rows
        self.methods = self.md.MethodDef.rows
        self.names = {}
        for i, t in enumerate(self.typedefs, 1):
            self.names[i] = self._s(t.TypeName)

    @staticmethod
    def _s(v):
        try:
            return str(v)
        except Exception:
            return repr(v)

    @staticmethod
    def refs(lst):
        return [getattr(x, "row_index", x) for x in (lst or [])]

    @staticmethod
    def sig(row):
        s = row.Signature
        for a in ("raw_data", "value", "data"):
            if hasattr(s, a):
                v = getattr(s, a)
                if isinstance(v, (bytes, bytearray)):
                    return bytes(v)
        return b""

    def coded_name(self, v):
        table = v & 3
        row = v >> 2
        if table == 0 and 1 <= row <= len(self.typedefs):
            return self.names.get(row, f"TypeDef#{row}")
        if table == 1 and 1 <= row <= len(self.typerefs):
            return self._s(self.typerefs[row - 1].TypeName)
        return f"spec#{row}"

    def type_name(self, coded):
        if hasattr(coded, "table"):
            row = coded.row_index
            if coded.table is self.md.TypeDef and 1 <= row <= len(self.typedefs):
                return self.names.get(row, f"TypeDef#{row}")
            if coded.table is self.md.TypeRef and 1 <= row <= len(self.typerefs):
                return self._s(self.typerefs[row - 1].TypeName)
            return f"other#{row}"
        try:
            return self.coded_name(int(coded))
        except Exception:
            return "?"

    @staticmethod
    def _cuint(data, i):
        b = data[i]
        if b & 0x80 == 0:
            return b, i + 1
        if b & 0xC0 == 0x80:
            return ((b & 0x3F) << 8) | data[i + 1], i + 2
        return ((b & 0x1F) << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3], i + 4

    def decode_type(self, data, i):
        e = data[i]
        i += 1
        if e in ELEMENT:
            return ELEMENT[e], i
        if e in (0x11, 0x12):
            v, i = self._cuint(data, i)
            return self.coded_name(v), i
        if e == 0x1D:
            inner, i = self.decode_type(data, i)
            return inner + "[]", i
        if e == 0x0F:
            inner, i = self.decode_type(data, i)
            return inner + "*", i
        if e == 0x15:
            inner, i = self.decode_type(data, i)
            n, i = self._cuint(data, i)
            args = []
            for _ in range(n):
                a, i = self.decode_type(data, i)
                args.append(a)
            return f"{inner}<{','.join(args)}>", i
        return f"elem0x{e:02x}", i

    def field_type(self, row):
        d = self.sig(row)
        if not d:
            return "?"
        # blob: <len><0x06><type>
        i = 1
        if d[i] != 0x06:
            return "?"
        t, _ = self.decode_type(d, i + 1)
        return t

    def method_sig(self, row):
        d = self.sig(row)
        if not d:
            return "?", []
        i = 1
        conv = d[i]
        i += 1
        if conv & 0x10:  # GENERIC
            _, i = self._cuint(d, i)
        n, i = self._cuint(d, i)
        ret, i = self.decode_type(d, i)
        params = []
        for _ in range(n):
            t, i = self.decode_type(d, i)
            params.append(t)
        return ret, params

    def find(self, *keywords):
        kws = [k.lower() for k in keywords]
        out = []
        for i, t in enumerate(self.typedefs, 1):
            name = self.names.get(i, "")
            if any(k in name.lower() for k in kws):
                out.append((i, name, t))
        return out

    def dump(self, idx, t, show_sig=True):
        print(f"\n=== {self.names.get(idx, '?')} ===")
        print(f"  extends: {self.type_name(t.Extends)}")
        for r in self.refs(t.FieldList):
            f = self.fields[r - 1]
            ft = self.field_type(f) if show_sig else ""
            print(f"  F {self._s(f.Name)} : {ft}")
        for r in self.refs(t.MethodList):
            m = self.methods[r - 1]
            if show_sig:
                ret, params = self.method_sig(m)
                print(f"  M {self._s(m.Name)}({', '.join(params)}) : {ret}")
            else:
                print(f"  M {self._s(m.Name)}")


if __name__ == "__main__":
    a = Asm()
    kws = sys.argv[1:] or ["llama"]
    for idx, name, t in a.find(*kws):
        a.dump(idx, t)
