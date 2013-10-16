﻿[<AutoOpen>]
/// Contains functions to read and write XML files.
module Fake.XMLHelper

open System
open System.Collections.Generic
open System.Text
open System.IO
open System.Xml
open System.Xml.XPath

/// Reads a value from a XML document using a XPath
let XMLRead failOnError (xmlFileName:string) nameSpace prefix xPath =
    try
        let document = new XPathDocument(xmlFileName)
        let navigator = document.CreateNavigator()
        let manager = new XmlNamespaceManager(navigator.NameTable)
        if prefix <> "" && nameSpace <> "" then manager.AddNamespace(prefix, nameSpace)
    
        let  expression = XPathExpression.Compile(xPath, manager)
        seq {
            match expression.ReturnType with
            | XPathResultType.Number 
            | XPathResultType.Boolean
            | XPathResultType.String  -> yield navigator.Evaluate(expression).ToString()
            | XPathResultType.NodeSet ->
                let nodes = navigator.Select(expression)            
                while nodes.MoveNext() do
                yield nodes.Current.Value
            | _ -> failwith <| sprintf "XPath-Expression return type %A not implemented" expression.ReturnType}
    with
    | exn -> if failOnError then failwith "XMLRead error:\n%s" exn.Message else Seq.empty

/// Reads a value from a XML document using a XPath
/// Returns if the value is an int and the value
let XMLRead_Int failOnError xmlFileName nameSpace prefix xPath =
    let headOrDefault def seq =
        if Seq.isEmpty seq then
            def
        else
            Seq.head seq
    XMLRead failOnError xmlFileName nameSpace prefix xPath
    |> Seq.map Int32.TryParse
    |> (fun seq -> if failOnError then Seq.head seq else headOrDefault (false, 0) seq)

  
/// Creates a XmlWriter which writes to the given file name
let XmlWriter (fileName:string) = 
    let writer = new XmlTextWriter(fileName, null)    
    writer.WriteStartDocument()
    writer

/// Writes an XML comment to the given XmlTextWriter
let XmlComment comment (writer:XmlTextWriter) =
    writer.WriteComment comment
    writer
  
/// Writes an XML start element to the given XmlTextWriter
let XmlStartElement name (writer:XmlTextWriter) =
    writer.WriteStartElement name
    writer
  
/// Writes an XML end element to the given XmlTextWriter
let XmlEndElement (writer:XmlTextWriter) =
    writer.WriteEndElement()
    writer        
  
/// Writes an XML attribute to current element of the given XmlTextWriter
let XmlAttribute name value (writer:XmlTextWriter) =
    writer.WriteAttributeString(name, value.ToString())
    writer    

/// Writes an CData element to the given XmlTextWriter
let XmlCDataElement elementName data (writer:XmlTextWriter) =
    XmlStartElement elementName writer |> ignore
    writer.WriteCData data
    XmlEndElement writer

/// Gets the attribute with the given name from the given XmlNode
let getAttribute (name:string) (node : #XmlNode) = node.Attributes.[name].Value

/// Gets a sequence of all child nodes for the given XmlNode
let getChilds (node : #XmlNode) = seq { for x in node.ChildNodes -> x }

/// Gets the first sub node with the given name from the given XmlNode
let getSubNode name node =
    getChilds node
      |> Seq.filter (fun x -> x.Name = name)
      |> Seq.head

/// Parses a XmlNode
let parse name f (node : #XmlNode) =
    if node.Name = name then f node else 
    failwithf "Could not parse %s - Node was %s" name node.Name

/// Parses a XML subnode
let parseSubNode name f =
    getSubNode name
      >> parse name f

/// Loads the given text into a XmlDocument
let XMLDoc text =
    if isNullOrEmpty text then null else
    let xmlDocument = new XmlDocument()
    xmlDocument.LoadXml text
    xmlDocument

/// Gets the DocumentElement of the XmlDocument
let DocElement (doc:XmlDocument) = doc.DocumentElement

/// Replaces text in the XML document specified by a XPath expression.
let XPathReplace xpath value (doc:XmlDocument) =
    let node = doc.SelectSingleNode xpath
    if node = null then failwithf "XML node '%s' not found" xpath else
    node.Value <- value
    doc

/// Selects a xml node value via XPath from the given document
let XPathValue xpath (namespaces:#seq<string * string>) (doc : XmlDocument) =
    let nsmgr = XmlNamespaceManager(doc.NameTable)
    namespaces |> Seq.iter nsmgr.AddNamespace
    let node = doc.DocumentElement.SelectSingleNode(xpath,nsmgr)
    if node = null then failwithf "XML node '%s' not found" xpath else
    node.InnerText

/// Replaces text in a XML file at the location specified by a XPath expression.
let XmlPoke (fileName:string) xpath value =
    let doc = new XmlDocument()
    doc.Load fileName
    XPathReplace xpath value doc
      |> fun x -> x.Save fileName

/// Replaces text in a XML document specified by a XPath expression, with support for namespaces.
let XPathReplaceNS xpath value (namespaces:#seq<string * string>) (doc:XmlDocument) =
    let nsmgr = XmlNamespaceManager(doc.NameTable)
    namespaces |> Seq.iter nsmgr.AddNamespace
    let node = doc.SelectSingleNode(xpath, nsmgr)
    if node = null then failwithf "XML node '%s' not found" xpath else

    node.Value <- value
    doc
 
/// Replaces text in a XML file at the location specified by a XPath expression, with support for namespaces.
let XmlPokeNS (fileName:string) namespaces xpath value =
    let doc = new XmlDocument()
    doc.Load fileName
    XPathReplaceNS xpath value namespaces doc
    |> fun x -> x.Save fileName