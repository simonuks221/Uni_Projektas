library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
use STD.textio.all;
use ieee.std_logic_textio.all;

entity SPI_DECODER is 
port(
	CLK: in std_logic := '0';
	RAW_CMD_DATA : in std_logic_vector(7 downto 0) := (others => '0');
	DECODED_CMD_DATA : out std_logic_vector(7 downto 0) := (others => '0');
	SPI_CYCLE_DONE : in std_logic := '0';
	RESET : in std_logic := '0';
	PACKET_STORAGE_EN : out std_logic := '0';
	STATUS_REGISTER_EN : out std_logic := '0';
	RTC_REGISTER_SET_EN : out std_logic := '0';
	RTC_REGISTER_READ_EN : out std_logic := '0'
);
end entity;

architecture arc of SPI_DECODER is

constant status_register_cmd : std_logic_vector(7 downto 0) := x"01";
constant packet_storage_cmd : std_logic_vector(7 downto 0) := x"02";
constant rtc_register_set_cmd : std_logic_vector(7 downto 0) := x"03";
constant rtc_register_read_cmd : std_logic_vector(7 downto 0) := x"04";

signal local_cmd : std_logic_vector(7 downto 0) := (others => '0');
signal cmd_active : std_logic := '0';
signal update_enables : std_logic := '0';
begin

process(CLK)
begin
	if rising_edge(CLK) then
		PACKET_STORAGE_EN <= '0';
		STATUS_REGISTER_EN <= '0';
		RTC_REGISTER_SET_EN <= '0';
		RTC_REGISTER_READ_EN <= '0';
		if update_enables = '1' then
			case local_cmd is
				when status_register_cmd =>
					STATUS_REGISTER_EN <= '1';
				when packet_storage_cmd =>
					PACKET_STORAGE_EN <= '1';
				when rtc_register_set_cmd =>
					RTC_REGISTER_SET_EN <= '1';
				when rtc_register_read_cmd =>
					RTC_REGISTER_READ_EN <= '1';
				when others =>
					--Do nothing
			end case;
		end if;
	end if;
end process;

process(CLK)
begin
	if rising_edge(CLK) then
		update_enables <= '0';
		if RESET = '1' then
			cmd_active <= '0';
			local_cmd <= (others => '0');
		else
			if SPI_CYCLE_DONE = '1' then
				if cmd_active = '0' then
					cmd_active <= '1';
					local_cmd <= RAW_CMD_DATA;
					update_enables <= '1';
				else
					DECODED_CMD_DATA <= RAW_CMD_DATA;
					update_enables <= '1';
				end if;
			end if;
		end if;
	end if;
end process;

end architecture;