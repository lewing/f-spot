// Copyright 2006 Alp Toker <alp@atoker.com>
// This software is made available under the MIT License
// See COPYING for details

using System;
using System.Text;

using System.Collections.Generic;
//TODO: Reflection should be done at a higher level than this class
using System.Reflection;

namespace NDesk.DBus
{
	//maybe this should be nullable?
	public struct Signature
	{
		//TODO: this class needs some work
		//Data should probably include the null terminator

		public static readonly Signature Empty = new Signature (String.Empty);

		public static bool operator == (Signature a, Signature b)
		{
			/*
			//TODO: remove this hack to handle bad case when Data is null
			if (a.data == null || b.data == null)
				throw new Exception ("Encountered Signature with null buffer");
			*/

			/*
			if (a.data == null && b.data == null)
				return true;

			if (a.data == null || b.data == null)
				return false;
			*/

			if (a.data.Length != b.data.Length)
				return false;

			for (int i = 0 ; i != a.data.Length ; i++)
				if (a.data[i] != b.data[i])
					return false;

			return true;
		}

		public static bool operator != (Signature a, Signature b)
		{
			return !(a == b);
		}

		public override bool Equals (object o)
		{
			if (o == null)
				return false;

			if (!(o is Signature))
				return false;

			return this == (Signature)o;
		}

		public override int GetHashCode ()
		{
			return data.GetHashCode ();
		}

		public static Signature operator + (Signature s1, Signature s2)
		{
			return Concat (s1, s2);
		}

		//these need to be optimized
		public static Signature Concat (Signature s1, Signature s2)
		{
			return new Signature (s1.Value + s2.Value);
		}

		public static Signature Copy (Signature sig)
		{
			return new Signature (sig.Value);
		}

		public Signature (string value)
		{
			this.data = Encoding.ASCII.GetBytes (value);
		}

		public Signature (byte[] value)
		{
			this.data = new byte[value.Length];

			for (int i = 0 ; i != value.Length ; i++)
				this.data[i] = value[i];
		}

		//this will become obsolete soon
		public Signature (DType value)
		{
			this.data = new byte[1];
			this.data[0] = (byte)value;
		}

		public Signature (DType[] value)
		{
			this.data = new byte[value.Length];

			/*
			MemoryStream ms = new MemoryStream (this.data);

			foreach (DType t in value)
				ms.WriteByte ((byte)t);
			*/

			for (int i = 0 ; i != value.Length ; i++)
				this.data[i] = (byte)value[i];
		}

		byte[] data;

		//TODO: this should be private, but MessageWriter and Monitor still use it
		//[Obsolete]
		public byte[] GetBuffer ()
		{
			return data;
		}

		public DType this[int index]
		{
			get {
				return (DType)data[index];
			}
		}

		public int Length
		{
			get {
				return data.Length;
			}
		}

		//[Obsolete]
		public string Value
		{
			get {
				/*
				//FIXME: hack to handle bad case when Data is null
				if (data == null)
					return String.Empty;
				*/

				return Encoding.ASCII.GetString (data);
			}
		}

		public override string ToString ()
		{
			StringBuilder sb = new StringBuilder ();

			foreach (DType t in data) {
				//we shouldn't rely on object mapping here, but it's an easy way to get string representations for now
				Type type = DTypeToType (t);
				if (type != null) {
					sb.Append (type.Name);
				} else {
					char c = (char)t;
					if (!Char.IsControl (c))
						sb.Append (c);
					else
						sb.Append (@"\" + (int)c);
				}
				sb.Append (" ");
			}

			return sb.ToString ();
		}

		public Signature MakeArraySignature ()
		{
			return new Signature (DType.Array) + this;
		}

		public static Signature MakeStruct (params Signature[] elems)
		{
			Signature sig = Signature.Empty;

			sig += new Signature (DType.StructBegin);

			foreach (Signature elem in elems)
				sig += elem;

			sig += new Signature (DType.StructEnd);

			return sig;
		}

		public static Signature MakeDictEntry (Signature keyType, Signature valueType)
		{
			Signature sig = Signature.Empty;

			sig += new Signature (DType.DictEntryBegin);

			sig += keyType;
			sig += valueType;

			sig += new Signature (DType.DictEntryEnd);

			return sig;
		}

		public static Signature MakeDict (Signature keyType, Signature valueType)
		{
			return MakeDictEntry (keyType, valueType).MakeArraySignature ();
		}

		/*
		//TODO: complete this
		public bool IsPrimitive
		{
			get {
				if (this == Signature.Empty)
					return true;

				return false;
			}
		}
		*/

		public bool IsDict
		{
			get {
				if (Length < 3)
					return false;

				if (!IsArray)
					return false;

				if (this[2] != DType.DictEntryBegin)
					return false;

				return true;
			}
		}

		public bool IsArray
		{
			get {
				if (Length < 2)
					return false;

				if (this[0] != DType.Array)
					return false;

				return true;
			}
		}

		public Signature GetElementSignature ()
		{
			if (!IsArray)
				throw new Exception ("Cannot get the element signature of a non-array (signature was '" + this + "')");

			//TODO: improve this
			if (Length != 2)
				throw new NotSupportedException ("Parsing signatures with more than one primitive value is not supported (signature was '" + this + "')");

			return new Signature (this[1]);
		}

		public Type ToType ()
		{
			if (this == Signature.Empty)
				return typeof (void);

			if (IsArray)
				return GetElementSignature ().ToType ().MakeArrayType ();

			if (Length == 1)
				return DTypeToType (this[0]);

			throw new NotSupportedException ("Parsing or converting this signature is not yet supported (signature was '" + this + "')");
		}

		public static DType TypeCodeToDType (TypeCode typeCode)
		{
			switch (typeCode)
			{
				case TypeCode.Empty:
					return DType.Invalid;
				case TypeCode.Object:
					return DType.Invalid;
				case TypeCode.DBNull:
					return DType.Invalid;
				case TypeCode.Boolean:
					return DType.Boolean;
				case TypeCode.Char:
					return DType.UInt16;
				case TypeCode.SByte:
					return DType.Byte;
				case TypeCode.Byte:
					return DType.Byte;
				case TypeCode.Int16:
					return DType.Int16;
				case TypeCode.UInt16:
					return DType.UInt16;
				case TypeCode.Int32:
					return DType.Int32;
				case TypeCode.UInt32:
					return DType.UInt32;
				case TypeCode.Int64:
					return DType.Int64;
				case TypeCode.UInt64:
					return DType.UInt64;
				case TypeCode.Single:
					return DType.Single;
				case TypeCode.Double:
					return DType.Double;
				case TypeCode.Decimal:
					return DType.Invalid;
				case TypeCode.DateTime:
					return DType.Invalid;
				case TypeCode.String:
					return DType.String;
				default:
					return DType.Invalid;
			}
		}

		public static DType TypeToDType (Type type)
		{
			if (type == typeof (void))
				return DType.Invalid;

			if (type == typeof (string))
				return DType.String;

			if (type == typeof (ObjectPath))
				return DType.ObjectPath;

			if (type == typeof (Signature))
				return DType.Signature;

			if (type == typeof (object))
				return DType.Variant;

			if (type.IsPrimitive)
				return TypeCodeToDType (Type.GetTypeCode (type));

			if (type.IsEnum)
				return TypeToDType (type.GetElementType ());

			//needs work
			if (type.IsArray)
				return DType.Array;

			if (!type.IsPrimitive && type.IsValueType && !type.IsEnum)
				return DType.Struct;

			//if (type.UnderlyingSystemType != null)
			//	return TypeToDType (type.UnderlyingSystemType);

			//TODO: maybe throw an exception here
			return DType.Invalid;
		}

		/*
		public static DType TypeToDType (Type type)
		{
			if (type == null)
				return DType.Invalid;
			else if (type == typeof (byte))
				return DType.Byte;
			else if (type == typeof (bool))
				return DType.Boolean;
			else if (type == typeof (short))
				return DType.Int16;
			else if (type == typeof (ushort))
				return DType.UInt16;
			else if (type == typeof (int))
				return DType.Int32;
			else if (type == typeof (uint))
				return DType.UInt32;
			else if (type == typeof (long))
				return DType.Int64;
			else if (type == typeof (ulong))
				return DType.UInt64;
			else if (type == typeof (float)) //FIXME: this isn't supported by DBus yet
				return DType.Single;
			else if (type == typeof (double))
				return DType.Double;
			else if (type == typeof (string))
				return DType.String;
			else if (type == typeof (ObjectPath))
				return DType.ObjectPath;
			else if (type == typeof (Signature))
				return DType.Signature;
			else
				return DType.Invalid;
		}
		*/

		public static Type DTypeToType (DType dtype)
		{
			switch (dtype) {
				case DType.Invalid:
					return typeof (void);
				case DType.Byte:
					return typeof (byte);
				case DType.Boolean:
					return typeof (bool);
				case DType.Int16:
					return typeof (short);
				case DType.UInt16:
					return typeof (ushort);
				case DType.Int32:
					return typeof (int);
				case DType.UInt32:
					return typeof (uint);
				case DType.Int64:
					return typeof (long);
				case DType.UInt64:
					return typeof (ulong);
				case DType.Single: //Not yet supported!
					return typeof (float);
				case DType.Double:
					return typeof (double);
				case DType.String:
					return typeof (string);
				case DType.ObjectPath:
					return typeof (ObjectPath);
				case DType.Signature:
					return typeof (Signature);
				case DType.Array:
					return typeof (Array);
				case DType.Struct:
					return typeof (ValueType);
				case DType.DictEntry:
					return typeof (System.Collections.Generic.KeyValuePair<,>);
				case DType.Variant:
					return typeof (object);
				default:
					return null;
			}
		}

		public static Signature GetSig (object[] objs)
		{
			return GetSig (Type.GetTypeArray (objs));
		}

		public static Signature GetSig (Type[] types)
		{
			if (types == null)
				throw new ArgumentNullException ("types");

			Signature sig = Signature.Empty;

			foreach (Type type in types)
					sig += GetSig (type);

			return sig;
		}

		public static Signature GetSig (Type type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			//this is inelegant, but works for now
			if (type == typeof (Signature))
				return new Signature (DType.Signature);

			if (type == typeof (ObjectPath))
				return new Signature (DType.ObjectPath);

			if (type == typeof (void))
				return Signature.Empty;

			if (type.IsArray)
				return GetSig (type.GetElementType ()).MakeArraySignature ();

			if (type.IsMarshalByRef) {
				//TODO: consider further what to do for remote object reference marshaling
				return new Signature (DType.ObjectPath);
			}
			
			if (type.IsGenericType && (type.GetGenericTypeDefinition () == typeof (IDictionary<,>) || type.GetGenericTypeDefinition () == typeof (Dictionary<,>))) {

				Type[] genArgs = type.GetGenericArguments ();
				return Signature.MakeDict (GetSig (genArgs[0]), GetSig (genArgs[1]));
			}

			if (!type.IsPrimitive && type.IsValueType && !type.IsEnum) {
				Signature sig = Signature.Empty;

				foreach (FieldInfo fi in type.GetFields ()) {
					//there didn't seem to be a way to do this with BindingFlags at time of writing
					if (fi.IsStatic)
						continue;
					sig += GetSig (fi.FieldType);
				}

				return Signature.MakeStruct (sig);
			}

			DType dtype = Signature.TypeToDType (type);
			return new Signature (dtype);
		}
	}

	public enum ArgDirection
	{
		In,
		Out,
	}

	public enum DType : byte
	{
		Invalid = (byte)'\0',

		Byte = (byte)'y',
		Boolean = (byte)'b',
		Int16 = (byte)'n',
		UInt16 = (byte)'q',
		Int32 = (byte)'i',
		UInt32 = (byte)'u',
		Int64 = (byte)'x',
		UInt64 = (byte)'t',
		Single = (byte)'f', //This is not yet supported!
		Double = (byte)'d',
		String = (byte)'s',
		ObjectPath = (byte)'o',
		Signature = (byte)'g',

		Array = (byte)'a',
		Struct = (byte)'r',
		DictEntry = (byte)'e',
		Variant = (byte)'v',

		StructBegin = (byte)'(',
		StructEnd = (byte)')',
		DictEntryBegin = (byte)'{',
		DictEntryEnd = (byte)'}',
	}
}
