/*
Matrix class in C#
Written by Ivan Kuckir (ivan.kuckir@gmail.com, http://blog.ivank.net)
Faculty of Mathematics and Physics
Charles University in Prague
(C) 2010
- updated on 1. 6.2014 - Trimming the string before parsing
- updated on 14.6.2012 - parsing improved. Thanks to Andy!
- updated on 3.10.2012 - there was a terrible b-g in LU, SoLE and Inversion. Thanks to Danilo Neves Cruz for reporting that!

This code is distributed under MIT licence.


    Permission is hereby granted, free of charge, to any person
    obtaining a copy of this software and associated documentation
    files (the "Software"), to deal in the Software without
    restriction, including without limitation the rights to use,
    copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the
    Software is furnished to do so, subject to the following
    conditions:

    The above copyright notice and this permission notice shall be
    included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
    OF MERCHANTABILITY, FITNESS FOR a PARTICULAR PURPOSE AND
    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
    HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
    WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
    OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Text.RegularExpressions;

namespace AcTools.Utils {
    internal class Matrix {
        public int Rows;
        public int Cols;
        public double[,] Mat;

        public Matrix L;
        public Matrix U;
        private int[] _pi;
        private double _detOfP = 1;

        public Matrix(int iRows, int iCols){
            Rows = iRows;
            Cols = iCols;
            Mat = new double[Rows, Cols];
        }

        public bool IsSquare => Rows == Cols;

        public double this[int iRow, int iCol]{
            get { return Mat[iRow, iCol]; }
            set { Mat[iRow, iCol] = value; }
        }

        public Matrix GetCol(int k) {
            var m = new Matrix(Rows, 1);
            for (var i = 0; i < Rows; i++) {
                m[i, 0] = Mat[i, k];
            }
            return m;
        }

        public void SetCol(Matrix v, int k) {
            for (var i = 0; i < Rows; i++) {
                Mat[i, k] = v[i, 0];
            }
        }

        public void MakeLu(){
            if (!IsSquare) throw new MathException("The matrix is not square!");
            L = IdentityMatrix(Rows, Cols);
            U = Duplicate();

            _pi = new int[Rows];
            for (var i = 0; i < Rows; i++) {
                _pi[i] = i;
            }

            var k0 = 0;

            for (var k = 0; k < Cols - 1; k++) {
                double p = 0;
                for (var i = k; i < Rows; i++){
                    if (!(Math.Abs(U[i, k]) > p)) continue;
                    p = Math.Abs(U[i, k]);
                    k0 = i;
                }

                if (Equals(p, 0d)) {
                    throw new MathException("The matrix is singular!");
                }

                var pom1 = _pi[k];
                _pi[k] = _pi[k0];
                _pi[k0] = pom1;

                double pom2;
                for (var i = 0; i < k; i++) {
                    pom2 = L[k, i];
                    L[k, i] = L[k0, i];
                    L[k0, i] = pom2;
                }

                if (k != k0) _detOfP *= -1;

                for (var i = 0; i < Cols; i++) {
                    pom2 = U[k, i];
                    U[k, i] = U[k0, i];
                    U[k0, i] = pom2;
                }

                for (var i = k + 1; i < Rows; i++) {
                    L[i, k] = U[i, k] / U[k, k];
                    for (var j = k; j < Cols; j++) {
                        U[i, j] = U[i, j] - L[i, k] * U[k, j];
                    }
                }
            }
        }

        public Matrix SolveWith(Matrix v) {
            if (Rows != Cols) throw new MathException("The matrix is not square!");
            if (Rows != v.Rows) throw new MathException("Wrong number of results in solution vector!");
            if (L == null) MakeLu();

            var b = new Matrix(Rows, 1);
            for (var i = 0; i < Rows; i++) {
                b[i, 0] = v[_pi[i], 0]; // switch two items in "v" due to permutation matrix
            }

            var z = SubsForth(L, b);
            var x = SubsBack(U, z);

            return x;
        }

        public Matrix Invert() {
            if (L == null) MakeLu();

            var inv = new Matrix(Rows, Cols);

            for (var i = 0; i < Rows; i++) {
                var ei = ZeroMatrix(Rows, 1);
                ei[i, 0] = 1;
                var col = SolveWith(ei);
                inv.SetCol(col, i);
            }
            return inv;
        }

        public double Det()  {
            if (L == null) MakeLu();
            var det = _detOfP;
            for (var i = 0; i < Rows; i++) {
                det *= U[i, i];
            }
            return det;
        }

        public Matrix GetP() {
            if (L == null) MakeLu();

            var matrix = ZeroMatrix(Rows, Cols);
            for (var i = 0; i < Rows; i++) {
                matrix[_pi[i], i] = 1;
            }
            return matrix;
        }

        public Matrix Duplicate() {
            var matrix = new Matrix(Rows, Cols);
            for (var i = 0; i < Rows; i++) {
                for (var j = 0; j < Cols; j++) {
                    matrix[i, j] = Mat[i, j];
                }
            }
            return matrix;
        }

        public static Matrix SubsForth(Matrix a, Matrix b){
            if (a.L == null) a.MakeLu();
            var n = a.Rows;
            var x = new Matrix(n, 1);

            for (var i = 0; i < n; i++) {
                x[i, 0] = b[i, 0];
                for (var j = 0; j < i; j++) {
                    x[i, 0] -= a[i, j] * x[j, 0];
                }
                x[i, 0] = x[i, 0] / a[i, i];
            }
            return x;
        }

        public static Matrix SubsBack(Matrix a, Matrix b) {
            if (a.L == null) a.MakeLu();
            var n = a.Rows;
            var x = new Matrix(n, 1);

            for (var i = n - 1; i > -1; i--) {
                x[i, 0] = b[i, 0];
                for (var j = n - 1; j > i; j--) {
                    x[i, 0] -= a[i, j] * x[j, 0];
                }
                x[i, 0] = x[i, 0] / a[i, i];
            }
            return x;
        }

        public static Matrix ZeroMatrix(int iRows, int iCols) {
            var matrix = new Matrix(iRows, iCols);
            for (var i = 0; i < iRows; i++) {
                for (var j = 0; j < iCols; j++) {
                    matrix[i, j] = 0;
                }
            }
            return matrix;
        }

        public static Matrix IdentityMatrix(int iRows, int iCols) {
            var matrix = ZeroMatrix(iRows, iCols);
            for (var i = 0; i < Math.Min(iRows, iCols); i++) {
                matrix[i, i] = 1;
            }
            return matrix;
        }

        public static Matrix RandomMatrix(int iRows, int iCols, int dispersion){
            var random = new Random();
            var matrix = new Matrix(iRows, iCols);
            for (var i = 0; i < iRows; i++) {
                for (var j = 0; j < iCols; j++) {
                    matrix[i, j] = random.Next(-dispersion, dispersion);
                }
            }
            return matrix;
        }

        public static Matrix Parse(string ps) {
            var s = NormalizeMatrixString(ps);
            var rows = Regex.Split(s, "\r\n");
            var nums = rows[0].Split(' ');
            var matrix = new Matrix(rows.Length, nums.Length);
            try {
                for (var i = 0; i < rows.Length; i++) {
                    nums = rows[i].Split(' ');
                    for (var j = 0; j < nums.Length; j++) {
                        matrix[i, j] = double.Parse(nums[j]);
                    }
                }
            } catch (FormatException) {
                throw new MathException("Wrong input format!");
            }
            return matrix;
        }

        public override string ToString(){
            var s = "";
            for (var i = 0; i < Rows; i++) {
                for (var j = 0; j < Cols; j++) {
                    s += $"{Mat[i, j],5:0.00}" + " ";
                }
                s += "\r\n";
            }
            return s;
        }

        public static Matrix Transpose(Matrix m){
            var t = new Matrix(m.Cols, m.Rows);
            for (var i = 0; i < m.Rows; i++) for (var j = 0; j < m.Cols; j++) t[j, i] = m[i, j];
            return t;
        }

        public static Matrix Power(Matrix m, int pow){
            switch (pow) {
                case 0:
                    return IdentityMatrix(m.Rows, m.Cols);
                case 1:
                    return m.Duplicate();
                case -1:
                    return m.Invert();
                default:
                    Matrix x;
                    if (pow < 0) {
                        x = m.Invert();
                        pow *= -1;
                    } else x = m.Duplicate();

                    var ret = IdentityMatrix(m.Rows, m.Cols);
                    while (pow != 0) {
                        if ((pow & 1) == 1) ret *= x;
                        x *= x;
                        pow >>= 1;
                    }
                    return ret;
            }
        }

        private static void SafeAplusBintoC(Matrix a, int xa, int ya, Matrix b, int xb, int yb, Matrix c, int size) {
            for (var i = 0; i < size; i++) {
                for (var j = 0; j < size; j++) {
                    c[i, j] = 0;
                    if (xa + j < a.Cols && ya + i < a.Rows) c[i, j] += a[ya + i, xa + j];
                    if (xb + j < b.Cols && yb + i < b.Rows) c[i, j] += b[yb + i, xb + j];
                }
            }
        }

        private static void SafeAminusBintoC(Matrix a, int xa, int ya, Matrix b, int xb, int yb, Matrix c, int size) {
            for (var i = 0; i < size; i++) {
                for (var j = 0; j < size; j++) {
                    c[i, j] = 0;
                    if (xa + j < a.Cols && ya + i < a.Rows) c[i, j] += a[ya + i, xa + j];
                    if (xb + j < b.Cols && yb + i < b.Rows) c[i, j] -= b[yb + i, xb + j];
                }
            }
        }

        private static void SafeACopytoC(Matrix a, int xa, int ya, Matrix c, int size) {
            for (var i = 0; i < size; i++) {
                for (var j = 0; j < size; j++) {
                    c[i, j] = 0;
                    if (xa + j < a.Cols && ya + i < a.Rows) {
                        c[i, j] += a[ya + i, xa + j];
                    }
                }
            }
        }

        private static void AplusBintoC(Matrix a, int xa, int ya, Matrix b, int xb, int yb, Matrix c, int size) {
            for (var i = 0; i < size; i++) {
                for (var j = 0; j < size; j++) {
                    c[i, j] = a[ya + i, xa + j] + b[yb + i, xb + j];
                }
            }
        }

        private static void AminusBintoC(Matrix a, int xa, int ya, Matrix b, int xb, int yb, Matrix c, int size) {
            for (var i = 0; i < size; i++) {
                for (var j = 0; j < size; j++) {
                    c[i, j] = a[ya + i, xa + j] - b[yb + i, xb + j];
                }
            }
        }

        private static void ACopytoC(Matrix a, int xa, int ya, Matrix c, int size) {
            for (var i = 0; i < size; i++) {
                for (var j = 0; j < size; j++) {
                    c[i, j] = a[ya + i, xa + j];
                }
            }
        }

        private static Matrix StrassenMultiply(Matrix a, Matrix b) {
            if (a.Cols != b.Rows) throw new MathException("Wrong dimension of matrix");

            Matrix r;

            var msize = Math.Max(Math.Max(a.Rows, a.Cols), Math.Max(b.Rows, b.Cols));
            if (msize < 32) {
                r = ZeroMatrix(a.Rows, b.Cols);
                for (var i = 0; i < r.Rows; i++) for (var j = 0; j < r.Cols; j++) for (var k = 0; k < a.Cols; k++) r[i, j] += a[i, k] * b[k, j];
                return r;
            }

            var size = 1;
            var n = 0;
            while (msize > size) {
                size *= 2;
                n++;
            }

            var h = size / 2;


            var mField = new Matrix[n, 9];

            /*
             *  8x8, 8x8, 8x8, ...
             *  4x4, 4x4, 4x4, ...
             *  2x2, 2x2, 2x2, ...
             *  . . .
             */

            for (var i = 0; i < n - 4; i++) {
                var z = (int)Math.Pow(2, n - i - 1);
                for (var j = 0; j < 9; j++) {
                    mField[i, j] = new Matrix(z, z);
                }
            }

            SafeAplusBintoC(a, 0, 0, a, h, h, mField[0, 0], h);
            SafeAplusBintoC(b, 0, 0, b, h, h, mField[0, 1], h);
            StrassenMultiplyRun(mField[0, 0], mField[0, 1], mField[0, 1 + 1], 1, mField); // (A11 + A22) * (B11 + B22);

            SafeAplusBintoC(a, 0, h, a, h, h, mField[0, 0], h);
            SafeACopytoC(b, 0, 0, mField[0, 1], h);
            StrassenMultiplyRun(mField[0, 0], mField[0, 1], mField[0, 1 + 2], 1, mField); // (A21 + A22) * B11;

            SafeACopytoC(a, 0, 0, mField[0, 0], h);
            SafeAminusBintoC(b, h, 0, b, h, h, mField[0, 1], h);
            StrassenMultiplyRun(mField[0, 0], mField[0, 1], mField[0, 1 + 3], 1, mField); //A11 * (B12 - B22);

            SafeACopytoC(a, h, h, mField[0, 0], h);
            SafeAminusBintoC(b, 0, h, b, 0, 0, mField[0, 1], h);
            StrassenMultiplyRun(mField[0, 0], mField[0, 1], mField[0, 1 + 4], 1, mField); //A22 * (B21 - B11);

            SafeAplusBintoC(a, 0, 0, a, h, 0, mField[0, 0], h);
            SafeACopytoC(b, h, h, mField[0, 1], h);
            StrassenMultiplyRun(mField[0, 0], mField[0, 1], mField[0, 1 + 5], 1, mField); //(A11 + A12) * B22;

            SafeAminusBintoC(a, 0, h, a, 0, 0, mField[0, 0], h);
            SafeAplusBintoC(b, 0, 0, b, h, 0, mField[0, 1], h);
            StrassenMultiplyRun(mField[0, 0], mField[0, 1], mField[0, 1 + 6], 1, mField); //(A21 - A11) * (B11 + B12);

            SafeAminusBintoC(a, h, 0, a, h, h, mField[0, 0], h);
            SafeAplusBintoC(b, 0, h, b, h, h, mField[0, 1], h);
            StrassenMultiplyRun(mField[0, 0], mField[0, 1], mField[0, 1 + 7], 1, mField); // (A12 - A22) * (B21 + B22);

            r = new Matrix(a.Rows, b.Cols); // result

            // C11
            for (var i = 0; i < Math.Min(h, r.Rows); i++) {
                // Rows
                for (var j = 0; j < Math.Min(h, r.Cols); j++) {
                    // Cols
                    r[i, j] = mField[0, 1 + 1][i, j] + mField[0, 1 + 4][i, j] - mField[0, 1 + 5][i, j] + mField[0, 1 + 7][i, j];
                }
            }

            // C12
            for (var i = 0; i < Math.Min(h, r.Rows); i++) {
                // Rows
                for (var j = h; j < Math.Min(2 * h, r.Cols); j++) {
                    // Cols
                    r[i, j] = mField[0, 1 + 3][i, j - h] + mField[0, 1 + 5][i, j - h];
                }
            }

            // C21
            for (var i = h; i < Math.Min(2 * h, r.Rows); i++) {
                // Rows
                for (var j = 0; j < Math.Min(h, r.Cols); j++) {
                    // Cols
                    r[i, j] = mField[0, 1 + 2][i - h, j] + mField[0, 1 + 4][i - h, j];
                }
            }

            // C22
            for (var i = h; i < Math.Min(2 * h, r.Rows); i++) {
                // Rows
                for (var j = h; j < Math.Min(2 * h, r.Cols); j++) {
                    // Cols
                    r[i, j] = mField[0, 1 + 1][i - h, j - h] - mField[0, 1 + 2][i - h, j - h] + mField[0, 1 + 3][i - h, j - h] + mField[0, 1 + 6][i - h, j - h];
                }
            }

            return r;
        }

        // function for square matrix 2^N x 2^N

        private static void StrassenMultiplyRun(Matrix a, Matrix b, Matrix c, int l, Matrix[,] f) {
            var size = a.Rows;
            var h = size / 2;

            if (size < 32) {
                for (var i = 0; i < c.Rows; i++) {
                    for (var j = 0; j < c.Cols; j++) {
                        c[i, j] = 0;
                        for (var k = 0; k < a.Cols; k++) {
                            c[i, j] += a[i, k] * b[k, j];
                        }
                    }
                }
                return;
            }

            AplusBintoC(a, 0, 0, a, h, h, f[l, 0], h);
            AplusBintoC(b, 0, 0, b, h, h, f[l, 1], h);
            StrassenMultiplyRun(f[l, 0], f[l, 1], f[l, 1 + 1], l + 1, f); // (A11 + A22) * (B11 + B22);

            AplusBintoC(a, 0, h, a, h, h, f[l, 0], h);
            ACopytoC(b, 0, 0, f[l, 1], h);
            StrassenMultiplyRun(f[l, 0], f[l, 1], f[l, 1 + 2], l + 1, f); // (A21 + A22) * B11;

            ACopytoC(a, 0, 0, f[l, 0], h);
            AminusBintoC(b, h, 0, b, h, h, f[l, 1], h);
            StrassenMultiplyRun(f[l, 0], f[l, 1], f[l, 1 + 3], l + 1, f); //A11 * (B12 - B22);

            ACopytoC(a, h, h, f[l, 0], h);
            AminusBintoC(b, 0, h, b, 0, 0, f[l, 1], h);
            StrassenMultiplyRun(f[l, 0], f[l, 1], f[l, 1 + 4], l + 1, f); //A22 * (B21 - B11);

            AplusBintoC(a, 0, 0, a, h, 0, f[l, 0], h);
            ACopytoC(b, h, h, f[l, 1], h);
            StrassenMultiplyRun(f[l, 0], f[l, 1], f[l, 1 + 5], l + 1, f); //(A11 + A12) * B22;

            AminusBintoC(a, 0, h, a, 0, 0, f[l, 0], h);
            AplusBintoC(b, 0, 0, b, h, 0, f[l, 1], h);
            StrassenMultiplyRun(f[l, 0], f[l, 1], f[l, 1 + 6], l + 1, f); //(A21 - A11) * (B11 + B12);

            AminusBintoC(a, h, 0, a, h, h, f[l, 0], h);
            AplusBintoC(b, 0, h, b, h, h, f[l, 1], h);
            StrassenMultiplyRun(f[l, 0], f[l, 1], f[l, 1 + 7], l + 1, f); // (A12 - A22) * (B21 + B22);

            // C11
            for (var i = 0; i < h; i++) {
                // Rows
                for (var j = 0; j < h; j++) {
                    // Cols
                    c[i, j] = f[l, 1 + 1][i, j] + f[l, 1 + 4][i, j] - f[l, 1 + 5][i, j] + f[l, 1 + 7][i, j];
                }
            }

            // C12
            for (var i = 0; i < h; i++) {// Rows
                for (var j = h; j < size; j++) {
                    // Cols
                    c[i, j] = f[l, 1 + 3][i, j - h] + f[l, 1 + 5][i, j - h];
                }
            }

        // C21
            for (var i = h; i < size; i++) {
                // Rows
                for (var j = 0; j < h; j++) {
                    // Cols
                    c[i, j] = f[l, 1 + 2][i - h, j] + f[l, 1 + 4][i - h, j];
                }
            }

            // C22
            for (var i = h; i < size; i++) {
                // Rows
                for (var j = h; j < size; j++) {
                    // Cols
                    c[i, j] = f[l, 1 + 1][i - h, j - h] - f[l, 1 + 2][i - h, j - h] + f[l, 1 + 3][i - h, j - h] + f[l, 1 + 6][i - h, j - h];
                }
            }
        }

        public static Matrix StupidMultiply(Matrix m1, Matrix m2){
            if (m1.Cols != m2.Rows) throw new MathException("Wrong dimensions of matrix!");

            var result = ZeroMatrix(m1.Rows, m2.Cols);
            for (var i = 0; i < result.Rows; i++) {
                for (var j = 0; j < result.Cols; j++) {
                    for (var k = 0; k < m1.Cols; k++) {
                        result[i, j] += m1[i, k] * m2[k, j];
                    }
                }
            }
            return result;
        }

        private static Matrix Multiply(double n, Matrix m) {
            var r = new Matrix(m.Rows, m.Cols);
            for (var i = 0; i < m.Rows; i++) {
                for (var j = 0; j < m.Cols; j++) {
                    r[i, j] = m[i, j] * n;
                }
            }
            return r;
        }

        private static Matrix Add(Matrix m1, Matrix m2) {
            if (m1.Rows != m2.Rows || m1.Cols != m2.Cols) throw new MathException("Matrices must have the same dimensions");
            var r = new Matrix(m1.Rows, m1.Cols);
            for (var i = 0; i < r.Rows; i++) {
                for (var j = 0; j < r.Cols; j++) {
                    r[i, j] = m1[i, j] + m2[i, j];
                }
            }
            return r;
        }

        public static string NormalizeMatrixString(string matStr){
            // Remove any multiple spaces
            while (matStr.IndexOf("  ", StringComparison.Ordinal) != -1) matStr = matStr.Replace("  ", " ");

            // Remove any spaces before or after newlines
            matStr = matStr.Replace(" \r\n", "\r\n");
            matStr = matStr.Replace("\r\n ", "\r\n");

            // If the data ends in a newline, remove the trailing newline.
            // Make it easier by first replacing \r\n’s with |’s then
            // restore the |’s with \r\n’s
            matStr = matStr.Replace("\r\n", "|");
            while (matStr.LastIndexOf("|", StringComparison.Ordinal) == matStr.Length - 1) {
                matStr = matStr.Substring(0, matStr.Length - 1);
            }

            matStr = matStr.Replace("|", "\r\n");
            return matStr.Trim();
        }
        
        public static Matrix operator -(Matrix m) {
            return Multiply(-1, m);
        }

        public static Matrix operator +(Matrix m1, Matrix m2) {
            return Add(m1, m2);
        }

        public static Matrix operator -(Matrix m1, Matrix m2) {
            return Add(m1, -m2);
        }

        public static Matrix operator *(Matrix m1, Matrix m2) {
            return StrassenMultiply(m1, m2);
        }

        public static Matrix operator *(double n, Matrix m) {
            return Multiply(n, m);
        }

        // For specific AC-related use
        public static Matrix Create(float[] array) {
            if (array.Length != 16) throw new NotSupportedException("Only 4×4 matrices supported");

            var matrix = new Matrix(4, 4);
            for (var i = 0; i < 4; i++) {
                for (var j = 0; j < 4; j++) {
                    matrix[i, j] = array[i * 4 + j];
                }
            }

            return matrix;
        }

        public float[] ToArray() {
            if (Mat.Length != 16 || !IsSquare) throw new NotSupportedException("Only 4×4 matrices supported");

            var result = new float[16];
            for (var i = 0; i < 4; i++) {
                for (var j = 0; j < 4; j++) {
                    result[i * 4 + j] = (float)this[i, j];
                }
            }

            return result;
        }
    }
}