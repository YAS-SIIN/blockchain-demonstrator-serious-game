﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlockchainDemonstratorApi.Data;
using BlockchainDemonstratorApi.Models.Classes;
using BlockchainDemonstratorApi.Models.Enums;
using Newtonsoft.Json;

namespace BlockchainDemonstratorApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BeerGameController : ControllerBase
    {
        private readonly BeerGameContext _context;

        public BeerGameController(BeerGameContext context)
        {
            _context = context;
        }

        [HttpPost("CreateGame")]
        public ActionResult CreateGame()
        {
            Game game = new Game(GetUniqueId());
            _context.Games.Add(game);
            _context.SaveChanges();
            return Ok();
        }

        /// <summary>Creates a unique id using six numbers</summary>
        /// <returns>Unique id as string</returns>
        /// <remarks>For now it returns a string later on, we might need to change that to an integer</remarks>
        private string GetUniqueId()
        {
            List<string> usedIds = _context.Games.Select(g => g.Id).ToList();

            Random r = new Random();
            while (true)
            {
                int id = r.Next(100000, 1000000);

                if (!usedIds.Contains(id.ToString()))
                {
                    return id.ToString();
                }
            }
        } 

        [HttpPost("JoinGame")]
        public ActionResult JoinGame([FromBody] dynamic data)
        {
            if (data.gameId.Value == "" || data.role.Value == "" || data.name.Value == "") return BadRequest();
            string gameId = (string) data.gameId;
            RoleType role = (RoleType) data.role;
            string name = (string) data.name;
            string playerId = (string) data.playerId;
            
            Game game = _context.Games.Find(gameId);
            if (game == null) return NotFound();

            bool joined = false;
            try
            {
                if (role == RoleType.Retailer)
                {
                    Player player = new Player(name, playerId);
                    player.Role = _context.Roles.FirstOrDefault(r => r.Id == "Retailer");
                    player.ChosenOption = _context.Options.FirstOrDefault(o => o.Name == "Basic" && o.RoleId == "Retailer");
                    game.Retailer = player;
                    joined = true;
                }
                else if (role == RoleType.Manufacturer)
                {
                    Player player = new Player(name, playerId);
                    player.Role = _context.Roles.FirstOrDefault(r => r.Id == "Manufacturer");
                    player.ChosenOption = _context.Options.FirstOrDefault(o => o.Name == "Basic" && o.RoleId == "Manufacturer");
                    game.Manufacturer = player;
                    joined = true;
                }
                else if (role == RoleType.Processor)
                {
                    Player player = new Player(name, playerId);
                    player.Role = _context.Roles.FirstOrDefault(r => r.Id == "Processor");
                    player.ChosenOption = _context.Options.FirstOrDefault(o => o.Name == "Basic" && o.RoleId == "Processor");
                    game.Processor = player;
                    joined = true;
                }
                else if (role == RoleType.Farmer)
                {
                    Player player = new Player(name, playerId);
                    player.Role = _context.Roles.FirstOrDefault(r => r.Id == "Farmer");
                    player.ChosenOption = _context.Options.FirstOrDefault(o => o.Name == "Basic" && o.RoleId == "Farmer");
                    game.Farmer = player;
                    joined = true;
                }
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }

            if (joined)
            {
                _context.Games.Update(game);
                _context.SaveChanges();
                return Ok();
            }

            return BadRequest();
        }

        [HttpPost("ChooseOption")]
        public ActionResult ChooseOption([FromBody] dynamic data)
        {
            if (data.option.Value == "" || data.playerId.Value == "") return BadRequest();

            string option = (string) data.option;
            string playerId = (string) data.playerId;

            var player = _context.Players.Include(x => x.Role).FirstOrDefault(x => x.Id == playerId);
            player.ChosenOption = _context.Options.FirstOrDefault(x => x.RoleId == player.Role.Id && x.Name == option);
            player.Payments.Add(new Payment
            {
                Amount = player.ChosenOption.CostOfStartUp * -1,
                DueDay = Factors.RoundIncrement * 8 + 1,
                FromPlayer = false,
                PlayerId = player.Id,
                Topic = "Setup " + player.ChosenOption.Name
            });
            _context.Players.Update(player);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("LeaveGame")]
        public void LeaveGame()
        {
            //TODO: add leave game method
        }

        // GET: api/BeerGame
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Game>>> GetGame()
        {
            return await _context.Games
                .Include(g => g.Retailer).ThenInclude(p => p.Role)
                .Include(g => g.Manufacturer).ThenInclude(p => p.Role)
                .Include(g => g.Processor).ThenInclude(p => p.Role)
                .Include(g => g.Farmer).ThenInclude(p => p.Role)
                .ToListAsync();
        }

        // POST: api/BeerGame/GetGame
        [HttpPost("GetGame")]
        public ActionResult<Game> GetGame([FromBody] string gameId)
        {
            if (gameId == "") return BadRequest();

            var game = _context.Games.FirstOrDefault(game => game.Id == gameId);

            if (game == null)
            {
                return NotFound();
            }
            return game;
        }

        // POST: api/BeerGame/SendOrders
        [HttpPost("SendOrders")]
        public ActionResult<Game> SendOrders([FromBody] dynamic data) //TODO: make singular later
        {
            if (data.gameId.Value == "") return BadRequest();

            string gameId = data.gameId;
            var game = GetGameFromContext(gameId);

            game.Retailer.CurrentOrder = new Order() {Volume = (data.retailerOrder.Value != "") ? Int32.Parse((string)data.retailerOrder) : 0 };
            game.Manufacturer.CurrentOrder = new Order() {Volume = (data.manufacturerOrder.Value != "") ? Int32.Parse((string)data.manufacturerOrder) : 0 };
            game.Processor.CurrentOrder = new Order() {Volume = (data.processorOrder.Value != "") ? Int32.Parse((string)data.processorOrder) : 0 };
            game.Farmer.CurrentOrder = new Order() {Volume = (data.farmerOrder.Value != "") ? Int32.Parse((string)data.farmerOrder) : 0 };

            game.Progress();
            _context.Games.Update(game);
            _context.SaveChanges();
            return game;
        }

        // PUT: api/BeerGame/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGame(string id, Game game)
        {
            if (id != game.Id)
            {
                return BadRequest();
            }

            _context.Entry(game).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GameExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/BeerGame
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<Game>> PostGame(Game game)
        {
            _context.Games.Add(game);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (GameExists(game.Id))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return CreatedAtAction("GetGame", new { id = game.Id }, game);
        }

        // DELETE: api/BeerGame/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Game>> DeleteGame(string id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound();
            }

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return game;
        }

        private bool GameExists(string id)
        {
            return _context.Games.Any(e => e.Id == id);
        }

        private Game GetGameFromContext(string gameId)
        {
            Game game = _context.Games.FirstOrDefault(game => game.Id == gameId);
            /*Seperated into chunks to reduce load time
            game.Retailer = _context.Games
                .Include(g => g.Retailer).ThenInclude(p => p.Role)
                .Include(g => g.Retailer).ThenInclude(p => p.CurrentOrder)
                .Include(g => g.Retailer).ThenInclude(p => p.IncomingOrders).ThenInclude(o => o.Deliveries)
                .Include(g => g.Retailer).ThenInclude(p => p.OutgoingOrders).ThenInclude(o => o.Deliveries)
                .FirstOrDefault(game => game.Id == gameId).Retailer;
            game.Manufacturer = _context.Games
                .Include(g => g.Manufacturer).ThenInclude(p => p.Role)
                .Include(g => g.Manufacturer).ThenInclude(p => p.CurrentOrder)
                .Include(g => g.Manufacturer).ThenInclude(p => p.IncomingOrders).ThenInclude(o => o.Deliveries)
                .Include(g => g.Manufacturer).ThenInclude(p => p.OutgoingOrders).ThenInclude(o => o.Deliveries)
                .FirstOrDefault(game => game.Id == gameId).Manufacturer;
            game.Processor = _context.Games
                .Include(g => g.Processor).ThenInclude(p => p.Role)
                .Include(g => g.Processor).ThenInclude(p => p.CurrentOrder)
                .Include(g => g.Processor).ThenInclude(p => p.IncomingOrders).ThenInclude(o => o.Deliveries)
                .Include(g => g.Processor).ThenInclude(p => p.OutgoingOrders).ThenInclude(o => o.Deliveries)
                .FirstOrDefault(game => game.Id == gameId).Processor;
            game.Farmer = _context.Games
                .Include(g => g.Farmer).ThenInclude(p => p.Role)
                .Include(g => g.Farmer).ThenInclude(p => p.CurrentOrder)
                .Include(g => g.Farmer).ThenInclude(p => p.IncomingOrders).ThenInclude(o => o.Deliveries)
                .Include(g => g.Farmer).ThenInclude(p => p.OutgoingOrders).ThenInclude(o => o.Deliveries)
                .FirstOrDefault(game => game.Id == gameId).Farmer;*/
            return game;
        }
    }
}
