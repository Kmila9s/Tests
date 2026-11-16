-- Test 15: Recursive Functions, Complex Expressions, and Advanced Arithmetic
-- Tests: Recursive functions, nested function calls, complex boolean expressions, shift operations

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

package MathFunctions is

    -- Recursive factorial function
    function factorial(n : integer) return integer;

    -- Recursive Fibonacci
    function fibonacci(n : integer) return integer;

    -- Recursive GCD (Euclidean algorithm)
    function gcd(a : integer; b : integer) return integer;

    -- Recursive power function
    function power(base : integer; exp : integer) return integer;

    -- Recursive binary to gray code conversion
    function bin_to_gray_recursive(bin : std_logic_vector; idx : integer) return std_logic_vector;

    -- Population count (recursive)
    function popcount(vec : std_logic_vector) return integer;

    -- Leading zero count (recursive)
    function clz(vec : std_logic_vector) return integer;

    -- Recursive bit reversal
    function reverse_bits(vec : std_logic_vector) return std_logic_vector;

    -- Complex expression functions
    function complex_condition(a, b, c, d : std_logic; mode : std_logic_vector(2 downto 0)) return std_logic;

    -- Nested arithmetic function
    function nested_arith(x, y, z : signed(15 downto 0); sel : std_logic_vector(1 downto 0)) return signed;

end MathFunctions;

package body MathFunctions is

    -- Recursive factorial: n! = n * (n-1)!
    function factorial(n : integer) return integer is
    begin
        if n <= 1 then
            return 1;
        else
            return n * factorial(n - 1);
        end if;
    end factorial;

    -- Recursive Fibonacci: fib(n) = fib(n-1) + fib(n-2)
    function fibonacci(n : integer) return integer is
    begin
        if n <= 0 then
            return 0;
        elsif n = 1 then
            return 1;
        else
            return fibonacci(n - 1) + fibonacci(n - 2);
        end if;
    end fibonacci;

    -- Recursive GCD using Euclidean algorithm
    function gcd(a : integer; b : integer) return integer is
    begin
        if b = 0 then
            return a;
        else
            return gcd(b, a mod b);
        end if;
    end gcd;

    -- Recursive power: base^exp
    function power(base : integer; exp : integer) return integer is
    begin
        if exp = 0 then
            return 1;
        elsif exp = 1 then
            return base;
        elsif (exp mod 2) = 0 then
            -- Even exponent: base^exp = (base^(exp/2))^2
            return power(base * base, exp / 2);
        else
            -- Odd exponent: base^exp = base * base^(exp-1)
            return base * power(base, exp - 1);
        end if;
    end power;

    -- Recursive binary to gray conversion
    function bin_to_gray_recursive(bin : std_logic_vector; idx : integer) return std_logic_vector is
        variable result : std_logic_vector(bin'range);
    begin
        if idx = bin'high then
            result := (others => '0');
            result(idx) := bin(idx);
            return result;
        else
            result := bin_to_gray_recursive(bin, idx + 1);
            result(idx) := bin(idx) xor bin(idx + 1);
            return result;
        end if;
    end bin_to_gray_recursive;

    -- Recursive population count (count 1s)
    function popcount(vec : std_logic_vector) return integer is
        variable mid : integer;
        variable left_count, right_count : integer;
    begin
        if vec'length = 0 then
            return 0;
        elsif vec'length = 1 then
            if vec(vec'low) = '1' then
                return 1;
            else
                return 0;
            end if;
        else
            mid := (vec'high + vec'low) / 2;
            left_count := popcount(vec(vec'high downto mid + 1));
            right_count := popcount(vec(mid downto vec'low));
            return left_count + right_count;
        end if;
    end popcount;

    -- Recursive leading zero count
    function clz(vec : std_logic_vector) return integer is
        variable mid : integer;
        variable upper_zeros : integer;
    begin
        if vec'length = 0 then
            return 0;
        elsif vec'length = 1 then
            if vec(vec'high) = '0' then
                return 1;
            else
                return 0;
            end if;
        else
            mid := (vec'high + vec'low + 1) / 2;
            -- Check upper half first
            upper_zeros := clz(vec(vec'high downto mid));
            if upper_zeros = vec'high - mid + 1 then
                -- All zeros in upper half, count lower half too
                return upper_zeros + clz(vec(mid - 1 downto vec'low));
            else
                return upper_zeros;
            end if;
        end if;
    end clz;

    -- Recursive bit reversal
    function reverse_bits(vec : std_logic_vector) return std_logic_vector is
        variable result : std_logic_vector(vec'range);
        constant half : integer := vec'length / 2;
    begin
        if vec'length <= 1 then
            return vec;
        else
            -- Swap halves and recurse
            result(vec'high downto vec'high - half + 1) :=
                reverse_bits(vec(vec'low + half - 1 downto vec'low));
            result(vec'high - half downto vec'low) :=
                reverse_bits(vec(vec'high downto vec'high - half + 1));
            return result;
        end if;
    end reverse_bits;

    -- Complex boolean expression function
    function complex_condition(a, b, c, d : std_logic; mode : std_logic_vector(2 downto 0)) return std_logic is
        variable result : std_logic;
    begin
        case mode is
            when "000" =>
                -- Complex AND-OR expression
                result := (a and b) or (c and d) or (a and c) or (b and d);
            when "001" =>
                -- XOR tree
                result := a xor b xor c xor d;
            when "010" =>
                -- Majority function
                result := (a and b) or (b and c) or (a and c);
            when "011" =>
                -- Implication chain: a -> b -> c -> d
                result := (not a or b) and (not b or c) and (not c or d);
            when "100" =>
                -- Complex nested expression
                result := ((a nand b) nor (c nand d)) xnor ((a xor c) and (b xor d));
            when "101" =>
                -- Parity with enables
                result := (a and (b xor c)) or (not a and (c xor d));
            when "110" =>
                -- Mux-like selection
                result := (a and b and not c) or (a and not b and c) or (not a and b and c) or (not a and not b and d);
            when others =>
                -- Default: NOR reduction
                result := not (a or b or c or d);
        end case;
        return result;
    end complex_condition;

    -- Nested arithmetic operations
    function nested_arith(x, y, z : signed(15 downto 0); sel : std_logic_vector(1 downto 0)) return signed is
        variable temp1, temp2, temp3 : signed(31 downto 0);
        variable result : signed(15 downto 0);
    begin
        -- Multiple nested operations based on selector
        case sel is
            when "00" =>
                -- (x + y) * z - x * y
                temp1 := (resize(x, 32) + resize(y, 32)) * resize(z, 32);
                temp2 := resize(x, 32) * resize(y, 32);
                temp3 := temp1 - temp2;
                result := temp3(15 downto 0);
            when "01" =>
                -- ((x - y) * (x + y)) / 2  (difference of squares, scaled)
                temp1 := resize(x, 32) - resize(y, 32);
                temp2 := resize(x, 32) + resize(y, 32);
                temp3 := shift_right(temp1 * temp2, 1);
                result := temp3(15 downto 0);
            when "10" =>
                -- Horner's method: x^2 + y*x + z = x*(x + y) + z
                temp1 := resize(x, 32) + resize(y, 32);
                temp2 := resize(x, 32) * temp1;
                temp3 := temp2 + resize(z, 32);
                result := temp3(15 downto 0);
            when others =>
                -- Average with rounding: (x + y + z + 1) / 3 approximation
                temp1 := resize(x, 32) + resize(y, 32) + resize(z, 32);
                -- Multiply by 0.34 approx using shift: (x * 11) >> 5
                temp2 := shift_right(temp1 * 11, 5);
                result := temp2(15 downto 0);
        end case;
        return result;
    end nested_arith;

end MathFunctions;

-- Test entity using the math functions
library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use work.MathFunctions.all;

entity RecursiveTestBench is
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- Factorial inputs/outputs
        fact_in : in std_logic_vector(3 downto 0);
        fact_out : out std_logic_vector(23 downto 0);
        -- Fibonacci inputs/outputs
        fib_in : in std_logic_vector(4 downto 0);
        fib_out : out std_logic_vector(15 downto 0);
        -- GCD inputs/outputs
        gcd_a : in std_logic_vector(7 downto 0);
        gcd_b : in std_logic_vector(7 downto 0);
        gcd_result : out std_logic_vector(7 downto 0);
        -- Popcount input/output
        pop_in : in std_logic_vector(15 downto 0);
        pop_out : out std_logic_vector(4 downto 0);
        -- CLZ input/output
        clz_in : in std_logic_vector(15 downto 0);
        clz_out : out std_logic_vector(4 downto 0);
        -- Complex condition
        cond_in : in std_logic_vector(3 downto 0);
        cond_mode : in std_logic_vector(2 downto 0);
        cond_out : out std_logic;
        -- Nested arithmetic
        arith_x, arith_y, arith_z : in std_logic_vector(15 downto 0);
        arith_sel : in std_logic_vector(1 downto 0);
        arith_out : out std_logic_vector(15 downto 0)
    );
end RecursiveTestBench;

architecture Behavioral of RecursiveTestBench is
    signal fact_reg : integer range 0 to 16777215 := 0;
    signal fib_reg : integer range 0 to 65535 := 0;
    signal gcd_reg : integer range 0 to 255 := 0;
begin
    process(clk, rst)
    begin
        if rst = '1' then
            fact_reg <= 0;
            fib_reg <= 0;
            gcd_reg <= 0;
            fact_out <= (others => '0');
            fib_out <= (others => '0');
            gcd_result <= (others => '0');
            pop_out <= (others => '0');
            clz_out <= (others => '0');
            cond_out <= '0';
            arith_out <= (others => '0');
        elsif rising_edge(clk) then
            -- Factorial calculation
            fact_reg <= factorial(to_integer(unsigned(fact_in)));
            fact_out <= std_logic_vector(to_unsigned(fact_reg, 24));

            -- Fibonacci calculation
            fib_reg <= fibonacci(to_integer(unsigned(fib_in)));
            fib_out <= std_logic_vector(to_unsigned(fib_reg, 16));

            -- GCD calculation
            gcd_reg <= gcd(to_integer(unsigned(gcd_a)), to_integer(unsigned(gcd_b)));
            gcd_result <= std_logic_vector(to_unsigned(gcd_reg, 8));

            -- Population count (combinational through function)
            pop_out <= std_logic_vector(to_unsigned(popcount(pop_in), 5));

            -- Leading zero count
            clz_out <= std_logic_vector(to_unsigned(clz(clz_in), 5));

            -- Complex condition evaluation
            cond_out <= complex_condition(cond_in(3), cond_in(2), cond_in(1), cond_in(0), cond_mode);

            -- Nested arithmetic
            arith_out <= std_logic_vector(nested_arith(
                signed(arith_x),
                signed(arith_y),
                signed(arith_z),
                arith_sel
            ));
        end if;
    end process;
end Behavioral;
