using System;
using System.CodeDom;
using System.IO;

namespace NStub.Core
{
    /// <summary>
    ///     The NStub type represents the container class which manages the
    ///     generation of testing code for the supplied
    ///     <see cref="System.CodeDom.CodeNamespace" /> using the
    ///     supplied implementation of
    ///     <see cref="NStub.Core.ICodeGenerator" />.
    ///     The resulting files are output to the given directory.
    /// </summary>
    public class NStubCore
    {
        #region Constructor (Public)

        /// <summary>
        ///     Initializes a new instance of the <see cref="NStubCore" /> class which
        ///     will generate the given <see cref="System.CodeDom.CodeNamespace" />
        ///     to the given output directory using the given implementation
        ///     of <see cref="NStub.Core.ICodeGenerator" />.
        /// </summary>
        /// <param name="codeNamespace">
        ///     The code namespace which contains the types
        ///     to be generated.
        /// </param>
        /// <param name="outputDirectory">
        ///     The directory where the resulting
        ///     files will be output to.
        /// </param>
        /// <param name="codeGenerator">
        ///     The code generator which will perform the
        ///     generation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        ///     codeNamespace,
        ///     outputDirectory, or codeGenerator is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     outputDirectory is an
        ///     empty string.
        /// </exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">
        ///     outputDirectory
        ///     is not a valid directory.
        /// </exception>
        public NStubCore( CodeNamespace codeNamespace, string outputDirectory, ICodeGenerator codeGenerator )
        {
            #region Validation

            // Null arguments will not be accepted
            if ( codeNamespace == null )
                throw new ArgumentNullException( "codeNamespace", Exceptions.ParameterCannotBeNull );
            if ( outputDirectory == null )
                throw new ArgumentNullException( "outputDirectory", Exceptions.ParameterCannotBeNull );
            if ( codeGenerator == null )
                throw new ArgumentNullException( "codeGenerator", Exceptions.ParameterCannotBeNull );
            // The output directory can't be empty
            if ( outputDirectory.Length == 0 )
                throw new ArgumentException( Exceptions.StringCannotBeEmpty, "outputDirectory" );
            // Ensure the output directory is valid
            if ( !Directory.Exists( outputDirectory ) )
                throw new DirectoryNotFoundException( Exceptions.DirectoryCannotBeFound );

            #endregion Validation

            // Set our member variables
            CodeNamespace = codeNamespace;
            OutputDirectory = outputDirectory;

            InitCodeGenerator( codeGenerator );
        }

        #endregion Constructor (Public)

        #region Methods (Public)

        /// <summary>
        ///     Generates the code represented by the current
        ///     <see cref="System.CodeDom.CodeNamespace" /> using
        ///     the current <see cref="NStub.Core.ICodeGenerator" />.
        /// </summary>
        public void GenerateCode( )
        {
            CodeGenerator.GenerateCode( );
        }

        #endregion Methods (Public)

        #region Helper Methods (Private)

        /// <summary>
        ///     Initializes the given <see cref="NStub.Core.ICodeGenerator">codeGenerator</see>.
        /// </summary>
        /// <param name="codeGenerator">
        ///     The
        ///     <see cref="NStub.Core.ICodeGenerator">codeGenerator</see> to be
        ///     initialized.
        /// </param>
        private void InitCodeGenerator( ICodeGenerator codeGenerator )
        {
            CodeGenerator = codeGenerator;
            CodeGenerator.CodeNamespace = CodeNamespace;
            CodeGenerator.OutputDirectory = OutputDirectory;
        }

        #endregion Helper Methods (Private)

        #region Fields (Private)

        #endregion Fields (Private)

        #region Properties (Public)

        /// <summary>
        ///     Gets or sets the <see cref="System.CodeDom.CodeNamespace" />
        ///     which contains the types to be generated.
        /// </summary>
        /// <value>
        ///     The <see cref="System.CodeDom.CodeNamespace" /> which
        ///     contains the types to be generated.
        /// </value>
        public CodeNamespace CodeNamespace { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="NStub.Core.ICodeGenerator" />
        ///     which will perform the actual generation of code.
        /// </summary>
        /// <value>
        ///     The <see cref="NStub.Core.ICodeGenerator" /> which will
        ///     perform the actual generation of code.
        /// </value>
        public ICodeGenerator CodeGenerator { get; set; }

        /// <summary>
        ///     Gets or sets the output directory where the resulting class files
        ///     will be placed.
        /// </summary>
        /// <value>
        ///     The output directory where the resulting class files will
        ///     be placed.
        /// </value>
        public string OutputDirectory { get; set; }

        #endregion Properties (Public)
    }
}