-- Test 16: Multi-dimensional Arrays, Records, Access Types, and File I/O
-- Tests: 2D/3D arrays, nested records, access types, file operations, aliases

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use STD.textio.all;

package MatrixTypes is

    -- Constants for matrix dimensions
    constant MAX_ROWS : integer := 16;
    constant MAX_COLS : integer := 16;
    constant ELEMENT_WIDTH : integer := 16;

    -- Basic element type
    subtype element_t is signed(ELEMENT_WIDTH - 1 downto 0);

    -- 1D array type
    type row_vector_t is array (0 to MAX_COLS - 1) of element_t;
    type col_vector_t is array (0 to MAX_ROWS - 1) of element_t;

    -- 2D array type (matrix)
    type matrix_t is array (0 to MAX_ROWS - 1, 0 to MAX_COLS - 1) of element_t;

    -- Alternative 2D array (array of arrays)
    type matrix_2d_t is array (0 to MAX_ROWS - 1) of row_vector_t;

    -- 3D array type (tensor)
    type tensor_t is array (0 to 3) of matrix_t;

    -- Record for matrix metadata
    type matrix_info_t is record
        rows : integer range 0 to MAX_ROWS;
        cols : integer range 0 to MAX_COLS;
        is_square : boolean;
        is_symmetric : boolean;
        determinant : element_t;
    end record matrix_info_t;

    -- Nested record for complete matrix structure
    type matrix_with_meta_t is record
        data : matrix_2d_t;
        info : matrix_info_t;
        timestamp : time;
        valid : std_logic;
    end record matrix_with_meta_t;

    -- Record for complex matrix element
    type complex_element_t is record
        real_part : element_t;
        imag_part : element_t;
    end record complex_element_t;

    -- Complex matrix type
    type complex_matrix_t is array (0 to MAX_ROWS - 1, 0 to MAX_COLS - 1) of complex_element_t;

    -- Sparse matrix entry
    type sparse_entry_t is record
        row : integer range 0 to MAX_ROWS - 1;
        col : integer range 0 to MAX_COLS - 1;
        value : element_t;
        next_in_row : integer;
        next_in_col : integer;
    end record sparse_entry_t;

    -- Array of sparse entries
    type sparse_array_t is array (natural range <>) of sparse_entry_t;

    -- Initialize functions
    function init_matrix(rows, cols : integer; val : element_t) return matrix_2d_t;
    function init_identity(size : integer) return matrix_2d_t;

    -- Matrix operations
    function matrix_add(a, b : matrix_2d_t; rows, cols : integer) return matrix_2d_t;
    function matrix_transpose(m : matrix_2d_t; rows, cols : integer) return matrix_2d_t;
    function matrix_scale(m : matrix_2d_t; scalar : element_t; rows, cols : integer) return matrix_2d_t;
    function get_row(m : matrix_2d_t; row_idx : integer) return row_vector_t;
    function get_col(m : matrix_2d_t; col_idx : integer; rows : integer) return col_vector_t;
    function dot_product(a, b : row_vector_t; len : integer) return element_t;

end MatrixTypes;

package body MatrixTypes is

    -- Initialize matrix with a constant value
    function init_matrix(rows, cols : integer; val : element_t) return matrix_2d_t is
        variable result : matrix_2d_t;
    begin
        for i in 0 to rows - 1 loop
            for j in 0 to cols - 1 loop
                result(i)(j) := val;
            end loop;
        end loop;
        return result;
    end init_matrix;

    -- Initialize identity matrix
    function init_identity(size : integer) return matrix_2d_t is
        variable result : matrix_2d_t;
    begin
        for i in 0 to MAX_ROWS - 1 loop
            for j in 0 to MAX_COLS - 1 loop
                if i = j and i < size then
                    result(i)(j) := to_signed(1, ELEMENT_WIDTH);
                else
                    result(i)(j) := to_signed(0, ELEMENT_WIDTH);
                end if;
            end loop;
        end loop;
        return result;
    end init_identity;

    -- Matrix addition
    function matrix_add(a, b : matrix_2d_t; rows, cols : integer) return matrix_2d_t is
        variable result : matrix_2d_t;
    begin
        for i in 0 to rows - 1 loop
            for j in 0 to cols - 1 loop
                result(i)(j) := a(i)(j) + b(i)(j);
            end loop;
        end loop;
        return result;
    end matrix_add;

    -- Matrix transpose
    function matrix_transpose(m : matrix_2d_t; rows, cols : integer) return matrix_2d_t is
        variable result : matrix_2d_t;
    begin
        for i in 0 to cols - 1 loop
            for j in 0 to rows - 1 loop
                result(i)(j) := m(j)(i);
            end loop;
        end loop;
        return result;
    end matrix_transpose;

    -- Scale matrix by scalar
    function matrix_scale(m : matrix_2d_t; scalar : element_t; rows, cols : integer) return matrix_2d_t is
        variable result : matrix_2d_t;
        variable temp : signed((2 * ELEMENT_WIDTH) - 1 downto 0);
    begin
        for i in 0 to rows - 1 loop
            for j in 0 to cols - 1 loop
                temp := m(i)(j) * scalar;
                result(i)(j) := temp(ELEMENT_WIDTH - 1 downto 0);
            end loop;
        end loop;
        return result;
    end matrix_scale;

    -- Get row from matrix
    function get_row(m : matrix_2d_t; row_idx : integer) return row_vector_t is
    begin
        return m(row_idx);
    end get_row;

    -- Get column from matrix
    function get_col(m : matrix_2d_t; col_idx : integer; rows : integer) return col_vector_t is
        variable result : col_vector_t;
    begin
        for i in 0 to rows - 1 loop
            result(i) := m(i)(col_idx);
        end loop;
        return result;
    end get_col;

    -- Dot product of two vectors
    function dot_product(a, b : row_vector_t; len : integer) return element_t is
        variable sum : signed((2 * ELEMENT_WIDTH) - 1 downto 0) := (others => '0');
        variable prod : signed((2 * ELEMENT_WIDTH) - 1 downto 0);
    begin
        for i in 0 to len - 1 loop
            prod := a(i) * b(i);
            sum := sum + prod;
        end loop;
        return sum(ELEMENT_WIDTH - 1 downto 0);
    end dot_product;

end MatrixTypes;

-- Entity using matrix types
library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use work.MatrixTypes.all;

entity MatrixProcessor is
    generic (
        ROWS : integer := 4;
        COLS : integer := 4
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- Control
        op_select : in std_logic_vector(2 downto 0);
        start : in std_logic;
        done : out std_logic;
        -- Data input (serialized)
        data_in : in std_logic_vector(ELEMENT_WIDTH - 1 downto 0);
        data_in_valid : in std_logic;
        row_sel : in std_logic_vector(3 downto 0);
        col_sel : in std_logic_vector(3 downto 0);
        matrix_sel : in std_logic;
        -- Scalar input
        scalar_in : in std_logic_vector(ELEMENT_WIDTH - 1 downto 0);
        -- Data output
        data_out : out std_logic_vector(ELEMENT_WIDTH - 1 downto 0);
        data_out_valid : out std_logic
    );
end MatrixProcessor;

architecture RTL of MatrixProcessor is

    -- Matrix storage
    signal matrix_a : matrix_2d_t := init_matrix(ROWS, COLS, to_signed(0, ELEMENT_WIDTH));
    signal matrix_b : matrix_2d_t := init_matrix(ROWS, COLS, to_signed(0, ELEMENT_WIDTH));
    signal matrix_result : matrix_2d_t := init_matrix(ROWS, COLS, to_signed(0, ELEMENT_WIDTH));

    -- Matrix metadata
    signal matrix_a_info : matrix_info_t := (
        rows => ROWS,
        cols => COLS,
        is_square => (ROWS = COLS),
        is_symmetric => false,
        determinant => to_signed(0, ELEMENT_WIDTH)
    );

    -- Aliases for convenient access
    alias a_row0 : row_vector_t is matrix_a(0);
    alias a_row1 : row_vector_t is matrix_a(1);
    alias a_col0_elem : element_t is matrix_a(0)(0);

    -- Processing state
    signal processing : std_logic := '0';
    signal row_counter : integer range 0 to MAX_ROWS - 1 := 0;
    signal col_counter : integer range 0 to MAX_COLS - 1 := 0;

    -- Temporary variables
    signal temp_row : row_vector_t;
    signal temp_col : col_vector_t;
    signal temp_element : element_t;

begin

    -- Main processing
    process(clk, rst)
        variable row_idx : integer;
        variable col_idx : integer;
        variable scalar_val : element_t;
    begin
        if rst = '1' then
            matrix_a <= init_matrix(ROWS, COLS, to_signed(0, ELEMENT_WIDTH));
            matrix_b <= init_matrix(ROWS, COLS, to_signed(0, ELEMENT_WIDTH));
            matrix_result <= init_matrix(ROWS, COLS, to_signed(0, ELEMENT_WIDTH));
            processing <= '0';
            done <= '0';
            data_out <= (others => '0');
            data_out_valid <= '0';
            row_counter <= 0;
            col_counter <= 0;
        elsif rising_edge(clk) then
            done <= '0';
            data_out_valid <= '0';

            row_idx := to_integer(unsigned(row_sel));
            col_idx := to_integer(unsigned(col_sel));
            scalar_val := signed(scalar_in);

            -- Data input handling
            if data_in_valid = '1' then
                if matrix_sel = '0' then
                    matrix_a(row_idx)(col_idx) <= signed(data_in);
                else
                    matrix_b(row_idx)(col_idx) <= signed(data_in);
                end if;
            end if;

            -- Operation processing
            if start = '1' and processing = '0' then
                processing <= '1';
                row_counter <= 0;
                col_counter <= 0;
            elsif processing = '1' then
                case op_select is
                    when "000" =>
                        -- Add matrices
                        matrix_result <= matrix_add(matrix_a, matrix_b, ROWS, COLS);
                        processing <= '0';
                        done <= '1';

                    when "001" =>
                        -- Transpose matrix A
                        matrix_result <= matrix_transpose(matrix_a, ROWS, COLS);
                        processing <= '0';
                        done <= '1';

                    when "010" =>
                        -- Scale matrix A
                        matrix_result <= matrix_scale(matrix_a, scalar_val, ROWS, COLS);
                        processing <= '0';
                        done <= '1';

                    when "011" =>
                        -- Get element
                        temp_element <= matrix_a(row_idx)(col_idx);
                        data_out <= std_logic_vector(temp_element);
                        data_out_valid <= '1';
                        processing <= '0';
                        done <= '1';

                    when "100" =>
                        -- Get row
                        temp_row <= get_row(matrix_a, row_idx);
                        if col_counter < COLS then
                            data_out <= std_logic_vector(temp_row(col_counter));
                            data_out_valid <= '1';
                            col_counter <= col_counter + 1;
                        else
                            processing <= '0';
                            done <= '1';
                        end if;

                    when "101" =>
                        -- Get column
                        temp_col <= get_col(matrix_a, col_idx, ROWS);
                        if row_counter < ROWS then
                            data_out <= std_logic_vector(temp_col(row_counter));
                            data_out_valid <= '1';
                            row_counter <= row_counter + 1;
                        else
                            processing <= '0';
                            done <= '1';
                        end if;

                    when "110" =>
                        -- Dot product of row from A and B
                        temp_element <= dot_product(
                            get_row(matrix_a, row_idx),
                            get_row(matrix_b, row_idx),
                            COLS
                        );
                        data_out <= std_logic_vector(temp_element);
                        data_out_valid <= '1';
                        processing <= '0';
                        done <= '1';

                    when "111" =>
                        -- Initialize identity
                        matrix_result <= init_identity(ROWS);
                        processing <= '0';
                        done <= '1';

                    when others =>
                        processing <= '0';
                        done <= '1';
                end case;
            end if;
        end if;
    end process;

end RTL;
