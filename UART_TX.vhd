--INspiracija:
--https://github.com/jakubcabal/uart-for-fpga/blob/master/rtl/comp/uart_tx.vhd

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
use STD.textio.all;
use ieee.std_logic_textio.all;

entity UART_TX is
generic(
	baud_rate : integer := 9600
);
port(
	CLK: in std_logic;
	UART_CLK : in std_logic;
	SEND_DATA : in std_logic_vector(7 downto 0);
	START_SEND_DATA : in std_logic;
	TX_BUSY : out std_logic := '0';
	TX : out std_logic
);
end entity;

architecture arc of UART_TX is
	type state is (idle, sync, start, data, stop);
	signal tx_curr_state : state := idle;
	signal tx_next_state : state := idle;
	
	signal data_index : integer range 0 to 7 := 0;
	signal data_send : std_logic_vector(7 downto 0) := (others => '0');
begin

--Set TX output
process(CLK)
begin
	if(rising_edge(CLK)) then
		case tx_curr_state is
			when start =>
				TX <= '0';
			when data =>
				TX <= data_send(data_index);
			when others => 
				--Stop sync and idle
				TX <= '1';
		end case;
	end if;
end process;

--Set next state
process(CLK)
begin
	if(rising_edge(CLK)) then
		tx_curr_state <= tx_next_state;
	end if;
end process;

--State management
process(UART_CLK)
begin
	if(rising_edge(UART_CLK)) then
		case tx_curr_state is
			when idle =>
				TX_BUSY <= '0';
				if(START_SEND_DATA = '1') then
					tx_next_state <= sync;
					TX_BUSY <= '1';
				end if;
			when sync =>
				data_send <= SEND_DATA; --Isirasom duomenis
				TX_BUSY <= '1';
				if(UART_CLK = '1') then
					tx_next_state <= start;
				end if;
			when start =>
				if(UART_CLK = '1') then
					tx_next_state <= data;
					data_index <= 0;
				end if;
			when data =>
				if(UART_CLK = '1') then
					if(data_index = 7) then
						tx_next_state <= stop;
					else
						data_index <= data_index + 1;
					end if;
				end if;
			when stop =>
				if(UART_CLK = '1') then
					tx_next_state <= idle;
				end if;
			when others =>
				tx_next_state <= idle;
				TX_BUSY <= '0';
		end case;
	end if;
end process;


end architecture;